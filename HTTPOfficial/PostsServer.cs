using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using RplaceServer;
using RplaceServer.Types;
using UnbloatDB;
using UnbloatDB.Serialisers;

namespace HTTPOfficial;

public class PostsServer
{
    private readonly WebApplication app;
    private readonly Database postsDb;
    private readonly RateLimiter postLimiter;
    private readonly Configuration configuration;
    public Action<string>? Logger;

    public PostsServer(Configuration config)
    {
        configuration = config;
        postsDb = new Database(new Config(configuration.PostsFolder, new JsonSerialiser()));
        postLimiter = new RateLimiter(TimeSpan.FromSeconds(configuration.PostLimitSeconds));
        
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy.WithOrigins(configuration.Origin, "*"));
        });

        builder.Configuration["Kestrel:Certificates:Default:Path"] = configuration.CertPath;
        builder.Configuration["Kestrel:Certificates:Default:KeyPath"] = configuration.KeyPath;
        
        app = builder.Build();
        app.Urls.Add($"{(configuration.UseHttps ? "https" : "http")}://*:{configuration.HttpPort}");
        app.UseCors(policy =>
        {
            policy.AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(_ => true)
                .AllowCredentials();
        });
        
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });
    }

    public async Task StartAsync()
    {
        app.MapGet("/posts", () =>
        {
            return Results.Json(postsDb.FindRecordsBefore<Post, DateTime>(nameof(Post.CreationDate), DateTime.Now, false));
        });

        app.MapGet("/posts/{masterKey}", (string masterKey) =>
        {
            return Results.Json(postsDb.GetRecord<Post>(masterKey));
        });
        
        app.MapPost("/posts/upload", async (Post submission, HttpContext context) =>
        {
            var address = context.Connection.RemoteIpAddress;
            
            if (address is null || !postLimiter.IsAuthorised(address))
            {
                Logger?.Invoke($"Client {address} denied post upload for breaching rate limit, or null address.");
                return Results.Unauthorized();
            }

            var sanitised = submission with
            {
                Upvotes = 0,
                Downvotes = 0,
                CreationDate = DateTime.Now,
                ContentPath = null,
            };

            // If client also wanted to upload content with this post, we give the post key, which gives them
            // temporary permission to upload the content to the CDN.
            var postKey = await postsDb.CreateRecord(sanitised);
            return Results.Text(postKey);
        });

        app.MapGet("/content/{contentPath}", (string contentPath) =>
        {
            var path = Path.Join(configuration.PostsFolder, "Content", contentPath);
            path = path.Replace("..", "");
            
            if (!File.Exists(path))
            {
                return Results.NotFound();
            }
            
            var stream = new FileStream(path, FileMode.Open);
            return Results.File(stream);
        });
        
        app.MapPost("/content/upload/{postKey}", async (HttpRequest request, string postKey, Stream body) =>
        {
            var address = request.HttpContext.Connection.RemoteIpAddress;
            var pendingPost = await postsDb.GetRecord<Post>(postKey);
            
            if (pendingPost is null || !pendingPost.MasterKey.Equals(postKey) || pendingPost.Data.ContentPath is not null)
            {
                Logger?.Invoke($"Client {address} denied content upload for invalid master key or post not found.");
                return Results.Unauthorized();
            }
            
            // Limit stream length to 5MB to prevent excessively large uploads
            if (request.ContentLength > 5_000_000)
            {
                Logger?.Invoke($"Client {address} denied content upload for too large stream file size.");
                return Results.UnprocessableEntity();
            }

            // Save data to CDN folder & update DB
            var contentPath = Path.Join(configuration.PostsFolder, "Content");
            if (!Directory.Exists(contentPath))
            {
                Directory.CreateDirectory(contentPath);
            }
            await using var fileStream = File.OpenWrite(Path.Join(contentPath, postKey));
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            await body.CopyToAsync(fileStream);
            
            pendingPost.Data.ContentPath = postKey;
            await postsDb.UpdateRecord(pendingPost);

            return Results.Ok();
        })
        .Accepts<IFormFile>("image/gif", "image/jpeg","image/png", "image/webp");

        await app.StartAsync();
    }
}