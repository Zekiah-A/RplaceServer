using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AuthOfficial.ApiModel;
using AuthOfficial.Configuration;
using AuthOfficial.DataModel;
using AuthOfficial.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthOfficial;

internal static partial class Program
{
    // /posts/upload
    [GeneratedRegex(@"^\/posts\/upload\/*$")]
    private static partial Regex PostUploadEndpointRegex();
    
    private static void MapPostEndpoints(this WebApplication app)
    {
        app.MapGet("/posts", ([FromQuery] DateTime? sinceDate, [FromQuery] DateTime? beforeDate,
            [FromQuery] int? beforeUpvotes, [FromQuery] int? sinceUpvotes, [FromQuery] int? beforeDownvotes,
            [FromQuery] int? sinceDownvotes, [FromQuery] int? authorId, [FromQuery] string? keyword,
            [FromQuery] int limit, DatabaseContext database) =>
        {
            var useLimit = Math.Clamp(limit, 1, 32);
            var query = database.Posts.AsQueryable();
            if (beforeDate.HasValue)
            {
                query = query.Where(post => post.CreationDate < beforeDate.Value)
                    .OrderByDescending(post => post.CreationDate);
            }
            if (sinceDate.HasValue)
            {
                query = query.Where(post => post.CreationDate > sinceDate.Value);
            }
            if (beforeUpvotes.HasValue)
            {
                query = query.Where(post => post.Upvotes < beforeUpvotes.Value)
                    .OrderByDescending(post => post.Upvotes);
            }
            if (sinceUpvotes.HasValue)
            {
                query = query.Where(post => post.Upvotes > beforeUpvotes.Value);
            }
            if (beforeDownvotes.HasValue)
            {
                query = query.Where(post => post.Downvotes < beforeDownvotes.Value)
                    .OrderByDescending(post => post.Downvotes);
            }
            if (sinceDownvotes.HasValue)
            {
                query = query.Where(post => post.Downvotes > beforeDownvotes.Value);
            }
            if (authorId.HasValue)
            {
                query = query.Where(post => post.AccountId == authorId);
            }
            if (!string.IsNullOrEmpty(keyword))
            {
                var searchKeyword = keyword.Trim().ToLower();
                query = query.Where(post => EF.Functions.Like(post.Title, $"%{searchKeyword}%")
                    || EF.Functions.Like(post.Description, $"%{searchKeyword}%"));
            }

            var posts = query.Include(post => post.Contents)
                .Take(useLimit)
                .ToList();
            return Results.Ok(new PostsResponse(posts.Count, posts));
        });
        
        app.MapGet("/posts/{id:int}", async (int id, DatabaseContext database) =>
        {
            if (await database.Posts.FindAsync(id) is not { } post)
            {
                return Results.NotFound(
                    new ErrorResponse("Specified post does not exist", "posts.notFound"));
            }
      		// Explicitly ensure that contents are fetched from navigation property
		    await database.Entry(post).Collection(postRecord => postRecord.Contents).LoadAsync();
            return Results.Ok(post);
        });

        app.MapPost("/posts/upload", async ([FromBody] PostUploadRequest submission, HttpContext context, CensorService censor, IOptionsSnapshot<PostsConfiguration> config, DatabaseContext database) =>
        {
            var userId = context.User.FindFirst("AccountId")?.Value;
            var canvasUserId = context.User.FindFirst("CanvasUserId")?.Value;

            if (userId is null && canvasUserId is null)
            {
                // new ErrorResponse("Authentication required", "post.upload.unauthorized")
                return Results.Unauthorized();
            }

            if (submission.Title.Length is < 1 or > 64)
            {
                return Results.BadRequest(
                    new ErrorResponse("Post title should be between 1-64 characters long", "post.upload.badTitleLength"));
            }
            if (submission.Description.Length is > 360)
            {
                return Results.BadRequest(
                    new ErrorResponse("Post description can not be longer than 360 characters", "post.upload.badDescriptionLength"));
            }

            var newPost = new Post(submission.Title, submission.Description)
            {
                Upvotes = 0,
                Downvotes = 0,
                CreationDate = DateTime.Now.ToUniversalTime(),
            };

            if (userId is not null)
            {
                newPost.AccountId = int.Parse(userId);
            }
            else if (canvasUserId is not null)
            {
                newPost.CanvasUserId = int.Parse(canvasUserId);
            }

            // Spam filtering
            newPost.Title = censor.CensorBanned(newPost.Title);
            newPost.Description = censor.CensorBanned(newPost.Description);
            
            // Automatic sensitive content detection - (Thanks to https://profanity.dev)
            if (await censor.ProbablyHasProfanity(newPost.Title) || await censor.ProbablyHasProfanity(newPost.Description))
            {
                newPost.HasSensitiveContent = true;
            }

            // If client also wanted to upload content with this post, we give the post key, which gives them
            // temporary permission to upload the content to the CDN.
            var uploadKey = RandomNumberGenerator.GetHexString(64, true);
            newPost.ContentUploadKey = uploadKey;

            await database.Posts.AddAsync(newPost);
            await database.SaveChangesAsync();

            return Results.Ok(new { PostId = newPost.Id, ContentUploadKey = uploadKey });
        })
        .RequireAuthorization()
        //.RateLimit(TimeSpan.FromSeconds(config.PostLimitSeconds))
        .RequireAuthType(AuthTypeFlags.Account | AuthTypeFlags.CanvasUser);

        app.MapGet("/posts/contents/{postContentId:int}", async (int postContentId, IOptionsSnapshot<PostsConfiguration> config, DatabaseContext database) =>
        {
            if (await database.PostContents.FindAsync(postContentId) is not { } postContent)
            {
                return Results.NotFound(new ErrorResponse("Specified post content could not be found",  "posts.content.contentNotFound"));
            }

            var contentPath = Path.Join(config.Value.PostsFolder, "Content", postContent.ContentKey);
            if (!File.Exists(contentPath))
            {
                return Results.NotFound(new ErrorResponse("Specified post content could not be found",  "posts.content.contentNotFound"));
            }

            var stream = File.OpenRead(contentPath);
            return Results.File(stream, postContent.ContentType, postContent.ContentKey);
        });

        app.MapPost("/posts/{id:int}/contents", async (int id, [FromForm] PostContentRequest request, HttpContext context, CensorService censor, IOptionsSnapshot<PostsConfiguration> config, DatabaseContext database) =>
        {
            var address = context.Connection.RemoteIpAddress;
            if (await database.Posts.FirstOrDefaultAsync(post => post.ContentUploadKey == request.ContentUploadKey)
                is not { } pendingPost)
            {
                return Results.Unauthorized();
            }
            if (pendingPost.ContentUploadKey is null)
            {
                return Results.Unauthorized();
            }

            // Limit stream length to 5MB to prevent excessively large uploads
            if (context.Request.ContentLength > 5_000_000)
            {
                return Results.UnprocessableEntity(new ErrorResponse("Provided content length was larger than maximum allowed size (5mb)",
                    "posts.content.tooLarge"));
            }
            
            // Save data to CDN folder & create content key
            var contentPath = Path.Join(config.Value.PostsFolder, "Content");
            if (!Directory.Exists(contentPath))
            {
                Directory.CreateDirectory(contentPath);
            }
            var extension = request.File.ContentType switch
            {
                "image/gif" => "gif",
                "image/jpeg" => "jpg",
                "image/png" =>  "png",
                "image/webp" => "webp",
                _ => null
            };
            if (extension is null)
            {
                logger.LogInformation("Client {address} denied content upload for invalid content type.", address);
                return Results.UnprocessableEntity(
                    new ErrorResponse("File was not of valid type 'image/gif, image/jpeg, image/png, image/webp'", "post.content.invalidType"));
            }

            // Explicitly ensure that contents are fetched from navigation property
            await database.Entry(pendingPost).Collection(post => post.Contents).LoadAsync();
            string contentKey;
            string contentFilePath;
            do
            {
                var contentIndex = pendingPost.Contents.Count + 1;
                contentKey = $"{pendingPost.Id}_{contentIndex}.{extension}";
                contentFilePath = Path.Combine(contentPath, contentKey);
            } while (File.Exists(contentFilePath));
            
            // Stream gymnastics to get the data into a byte array for the AI model, the content hasher,
            // and destination file
            await using var memoryStream = new MemoryStream();
            await request.File.CopyToAsync(memoryStream);
            await memoryStream.FlushAsync();
            
            // Banned content match check
            memoryStream.Seek(0L, SeekOrigin.Begin);
            if (censor.IsContentBanned(memoryStream, request.File.ContentType))
            {
                logger.LogTrace("Denied uploading post content {contentKey} from {address}, banned content hash match detected",
                    contentKey, address);
                return Results.Forbid();
            }
            
            // Content filters
            try
            {
                var result = await nudeNetAiService.RunModel(memoryStream.ToArray());
                if (result is null)
                {
                    logger.LogWarning("Error running CensorCore model on post content {contentKey}, result was null",
                        contentKey);
                }
                else
                {
                    if (result.Results.Count > 0)
                    {
                        pendingPost.HasSensitiveContent = true;
                    }

                    var session = result.Session;
                    if (session is not null)
                    {
                        logger.LogTrace("Ran CensorCore on post content {contentKey}, {Image} {Tensor} {Model} from {address}",
                            contentKey, session.ImageLoadTime, session.TensorLoadTime, session.ModelRunTime, address);
                    }
                }
            }
            catch (Exception exception)
            {
                logger.LogError("Error running CensorCore model on post content {contentKey}, {exception}",
                    contentKey, exception);
            }
            
            // Save to file
            memoryStream.Seek(0, SeekOrigin.Begin);
            await using var fileStream = File.OpenWrite(contentFilePath);
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            await memoryStream.CopyToAsync(fileStream);
            await memoryStream.FlushAsync();
            
            var postContent = new PostContent(contentKey, request.File.ContentType, id);
            await database.PostContents.AddAsync(postContent);
            await database.SaveChangesAsync();
            return Results.Ok();
        })
        .RequireAuthorization()
        .RequireAuthType(AuthTypeFlags.Account | AuthTypeFlags.CanvasUser)
        // TODO: Harden this if necessary, https://andrewlock.net/exploring-the-dotnet-8-preview-form-binding-in-minimal-apis
        .DisableAntiforgery();
    }
}
