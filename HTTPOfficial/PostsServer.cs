using Microsoft.AspNetCore.HttpOverrides;

namespace HTTPOfficial;

public class PostsServer
{
    private readonly WebApplication app;
    private readonly DatabaseContext postsDb;
    private readonly RateLimiter postLimiter;
    private readonly Configuration configuration;
    private Dictionary<string, int> contentUploadKeys; // key : postId
    public Action<string>? Logger;

    public PostsServer(Configuration config, DatabaseContext databaseContext)
    {
        configuration = config;
        postsDb = databaseContext;
        postLimiter = new RateLimiter(TimeSpan.FromSeconds(configuration.PostLimitSeconds));
        contentUploadKeys = new Dictionary<string, int>();
        
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
        app.MapGet("/posts/since/{fromDate}", (DateTime fromDate) =>
        {
            return Results.Ok(postsDb.Posts.Where(post => post.CreationDate > fromDate).Take(10));
        });

        app.MapGet("posts/before/{beforeDate}", (DateTime beforeDate) =>
        {
            return Results.Ok(postsDb.Posts.Where(post => post.CreationDate < beforeDate).Take(10));
        });

        app.MapGet("/posts/{id}", async (int id) =>
        {
            if (await postsDb.Posts.FindAsync(id) is not Post post)
            {
                return Results.NotFound();
            }
            else
            {
                return Results.Ok(post);
            }
        });
        
        app.MapPost("/posts/upload", async (PostUploadRequest submission, HttpContext context) =>
        {
            var address = context.Connection.RemoteIpAddress;
            
            if (address is null || !postLimiter.IsAuthorised(address))
            {
                Logger?.Invoke($"Client {address} denied post upload for breaching rate limit, or null address.");
                return Results.Unauthorized();
            }

            var sanitised = new Post(submission.Username, submission.Title, submission.Description)
            {
                Upvotes = 0,
                Downvotes = 0,
                CreationDate = DateTime.Now,
            };

            await postsDb.Posts.AddAsync(sanitised);
            await postsDb.SaveChangesAsync();

            // If client also wanted to upload content with this post, we give the post key, which gives them
            // temporary permission to upload the content to the CDN.
            var uploadKey = Guid.NewGuid().ToString();
            contentUploadKeys.Add(uploadKey, sanitised.Id);
            return Results.Ok(new { PostId = sanitised.Id, UploadKey = uploadKey });
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
        
        app.MapPost("/content/upload/{postKey}", async (HttpRequest request, string uploadKey, Stream body) =>
        {
            var address = request.HttpContext.Connection.RemoteIpAddress;
            if  (!contentUploadKeys.TryGetValue(uploadKey, out var pendingPostId)
                || await postsDb.Posts.FindAsync(1) is not Post pendingPost)
            {
                Logger?.Invoke($"Client {address} denied content upload for invalid master key or post not found.");
                return Results.Unauthorized();
            }
            else if (pendingPost.ContentPath is not null)
            {
                return Results.Unauthorized();
            }

            // Limit stream length to 5MB to prevent excessively large uploads
            if (request.ContentLength > 5_000_000)
            {
                Logger?.Invoke($"Client {address} denied content upload for too large stream file size.");
                return Results.UnprocessableEntity();
            }


            // Save data to CDN folder & create content key
            var contentPath = Path.Join(configuration.PostsFolder, "Content");
            if (!Directory.Exists(contentPath))
            {
                Directory.CreateDirectory(contentPath);
            }
            var extension = request.ContentType switch
            {
                "image/gif" => ".gif",
                "image/jpeg" => ".jpg",
                "image/png" =>  ".png",
                "image/webp" => ".webp",
                _ => null
            };
            if (extension is null)
            {
                Logger?.Invoke($"Client {address} denied content upload for invalid content type.");
                return Results.UnprocessableEntity();
            }
            var contentKey = pendingPostId + extension;
            await using var fileStream = File.OpenWrite(Path.Join(contentPath, contentKey));
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            await body.CopyToAsync(fileStream);

            pendingPost.ContentPath = contentKey;
            await postsDb.SaveChangesAsync();
            return Results.Ok();
        })
        .Accepts<IFormFile>("image/gif", "image/jpeg","image/png", "image/webp");

        await app.StartAsync();
    }
}