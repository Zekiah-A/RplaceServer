using HTTPOfficial.ApiModel;
using HTTPOfficial.DataModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HTTPOfficial;

internal static partial class Program
{
    private static void ConfigurePostEndpoints()
    {
        var postLimiter = new RateLimiter(TimeSpan.FromSeconds(config.PostLimitSeconds));
        app.MapGet("/posts/since/{fromDate:datetime}", (DateTime fromDate, DatabaseContext database) =>
        {
            return Results.Ok(database.Posts.Include(post => post.Contents)
            	.Where(post => post.CreationDate > fromDate).Take(10));
        });

        app.MapGet("/posts/before/{beforeDate:datetime}", (DateTime beforeDate, DatabaseContext postsDb) =>
        {
            return Results.Ok(postsDb.Posts.Include(post => post.Contents)
            	.Where(post => post.CreationDate < beforeDate).Take(10));
        });

        app.MapGet("/posts/{id:int}", async (int id, DatabaseContext database) =>
        {
            if (await database.Posts.FindAsync(id) is not { } post)
            {
                return Results.NotFound(
                    new ErrorResponse("Specified post does not exist", "posts.notFound"));
            }
      		// Explicitly ensure that contents are fetched from navigation property
		    database.Entry(post)
        		.Collection(post => post.Contents)
      			.Load();
      		
            return Results.Ok(post);
        });

        app.MapPost("/posts/upload", async ([FromBody] PostUploadRequest submission, HttpContext context, DatabaseContext database) =>
        {
            var address = context.Connection.RemoteIpAddress;
            if (address is null || !postLimiter.IsAuthorised(address))
            {
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
                CreationDate = DateTime.Now,
            };

            var postAccount = submission.AccountId is not null
                ? await database.Accounts.FindAsync(submission.AccountId)
                : null;
            if (postAccount is not null && submission.CanvasUser is not null)
            {
                return Results.BadRequest(new ErrorResponse("Provided username and account are mutually exclusive", "post.upload.exclusive"));
            }

            if (postAccount is not null)
            {
                newPost.AccountId = postAccount.Id;
            }
            else if (submission.CanvasUser is not null)
            {
                // We need to perform
                var userIntId = await VerifyCanvasUserIntId(submission.CanvasUser.InstanceId, submission.CanvasUser.LinkKey, database);
                if (userIntId is null)
                {
                    // User did not own this userId, or could not be authorised by canvas server
                    return Results.Unauthorized();
                }

                // Lookup if there is a CanvasUser with this intId, if not we can create it
                int? canvasUserId = null;
                var canvasUser = await database.CanvasUsers.FirstOrDefaultAsync(user => user.UserIntId == userIntId);
                if (canvasUser is null)
                {
                    var newCanvasUser = new CanvasUser()
                    {
                        InstanceId = submission.CanvasUser.InstanceId,
                        UserIntId = (int) userIntId
                    };
                    await database.CanvasUsers.AddAsync(newCanvasUser);
                    await database.SaveChangesAsync();
                    // Use canvas user ID from newly created record
                    canvasUserId = newCanvasUser.Id;
                }
                else
                {
                    canvasUserId = canvasUser.Id;
                }

                newPost.CanvasUserId = canvasUserId;
            }
            else
            {
                return Results.BadRequest(new ErrorResponse("No username or account provided", "post.upload.noUsernameOrAccount"));
            }
            
            // Automatic sensitive content detection - (Thanks to https://profanity.dev)
            if (await ProbablyHasProfanity(newPost.Title) || await ProbablyHasProfanity(newPost.Description))
            {
                newPost.HasSensitiveContent = true;
            }

            // If client also wanted to upload content with this post, we give the post key, which gives them
            // temporary permission to upload the content to the CDN.
            var uploadKey = Guid.NewGuid().ToString();
            newPost.ContentUploadKey = uploadKey;

            await database.Posts.AddAsync(newPost);
            await database.SaveChangesAsync();

            return Results.Ok(new { PostId = newPost.Id, ContentUploadKey = uploadKey });
        });

        app.MapGet("/posts/content/{postContentId:int}", async (int postContentId, DatabaseContext database) =>
        {
            if (await database.PostContents.FindAsync(postContentId) is not { } postContent)
            {
                return Results.NotFound(new ErrorResponse("Speficied post content could not be found",  "posts.content.contentNotFound"));
            }

            var contentPath = Path.Join(config.PostsFolder, "Content", postContent.ContentKey);
            if (!File.Exists(contentPath))
            {
                return Results.NotFound(new ErrorResponse("Speficied post content could not be found",  "posts.content.contentNotFound"));
            }

            var stream = File.OpenRead(contentPath);
            return Results.File(stream, postContent.ContentType, postContent.ContentKey);
        });

        app.MapPost("/posts/{id:int}/content", async (int id, [FromForm] PostContentRequest request, HttpContext context, DatabaseContext database) =>
        {
            var address = context.Connection.RemoteIpAddress;
            if (await database.Posts.FirstOrDefaultAsync(post => post.ContentUploadKey == request.ContentUploadKey) is not { } pendingPost)
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
                return Results.UnprocessableEntity(
                    new ErrorResponse("Provided content length was larger than maximum allowed size (5mb)", "posts.content.tooLarge"));
            }

            // Save data to CDN folder & create content key
            var contentPath = Path.Join(config.PostsFolder, "Content");
            if (!Directory.Exists(contentPath))
            {
                Directory.CreateDirectory(contentPath);
            }
            var extension = request.File.ContentType switch
            {
                "image/gif" => ".gif",
                "image/jpeg" => ".jpg",
                "image/png" =>  ".png",
                "image/webp" => ".webp",
                _ => null
            };
            if (extension is null)
            {
                logger.LogInformation($"Client {address} denied content upload for invalid content type.");
                return Results.UnprocessableEntity(
                    new ErrorResponse("File was not of valid type 'image/gif, image/jpeg, image/png, image/webp'", "post.content.invalidType"));
            }
            var contentKey = $"{pendingPost.Id}_{Guid.NewGuid().ToString()}{extension}";
            await using var fileStream = File.OpenWrite(Path.Join(contentPath, contentKey));
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.SetLength(0);
            await request.File.CopyToAsync(fileStream);

            var postContent = new PostContent(contentKey, request.File.ContentType, id);
            await database.PostContents.AddAsync(postContent);
            await database.SaveChangesAsync();
            return Results.Ok();
        })
        // TODO: Harden this if necessary, https://andrewlock.net/exploring-the-dotnet-8-preview-form-binding-in-minimal-apis
        .DisableAntiforgery();
    }

    private static async Task<bool> ProbablyHasProfanity(string text)
    {
        const string logPrefix = "Failed to query profanity API:";
        
        try
        {
            var result = await httpClient.PostAsJsonAsync("https://vector.profanity.dev",
                new ProfanityRequest(text), defaultJsonOptions);
            if (!result.IsSuccessStatusCode)
            {
                logger.LogError("{logPrefix} Status {statusCode} received", logPrefix, result.StatusCode);
                return false;
            }

            var profanityResponse = await result.Content.ReadFromJsonAsync<ProfanityResponse>();
            if (profanityResponse is null)
            {
                logger.LogError("{logPrefix} Deserialised  profanity response {statusCode} received", logPrefix, result.StatusCode);
                return false;
            }
            return profanityResponse.IsProfanity;
        }
        catch (Exception error)
        {
            logger.LogError("{logPrefix} {error}", logPrefix, error);
            return false;
        }
    }
    
    // Uses the linkage API to prove that with a given link key, a client owns an instance user account
    private static async Task<int?> VerifyCanvasUserIntId(int instanceId, string linkKey, DatabaseContext database)
    {
        var logPrefix = $"Could not verify canvas user with instanceId {instanceId}.";

        // Lookup instance
        var instance = await database.Instances.FindAsync(instanceId);
        if (instance is null)
        {
            logger.LogInformation("{logPrefix} Instance not found", logPrefix);
            return null;
        }

        // Verify with instance that they do own given link key
        var instanceUri = (instance.UsesHttps ? "https://" : "http://") + instance.ServerLocation;
        var linkVerifyUri = $"{instanceUri}/link/{linkKey}";
        var linkResponse = await httpClient.GetAsync(linkVerifyUri);
        if (linkResponse.IsSuccessStatusCode)
        {
            var linkData = await linkResponse.Content.ReadFromJsonAsync<LinkData>(defaultJsonOptions);
            if (linkData is null)
            {
                logger.LogInformation("{logPrefix} JSON data returned by server was invalid", logPrefix);
                return null;
            }

            // The user's int ID according to the canvas server
            return linkData.IntId;
        }

        logger.LogInformation("{logPrefix} Server denied linkage request ({statusCode} {content})",
            logPrefix, linkResponse.StatusCode, await linkResponse.Content.ReadAsStringAsync());
        return null;
    }
}
