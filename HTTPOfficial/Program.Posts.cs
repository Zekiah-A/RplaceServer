using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using HTTPOfficial.ApiModel;
using HTTPOfficial.DataModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace HTTPOfficial;

internal static partial class Program
{
    // /posts/upload
    [GeneratedRegex(@"^\/posts\/upload\/*$")]
    private static partial Regex PostUploadEndpointRegex();
    
    [GeneratedRegex(@"https?:\/\/(\w+\.)+\w{2,15}(\/\S*)?|(\w+\.)+\w{2,15}\/\S*|(\w+\.)+(tk|ga|gg|gq|cf|ml|fun|xxx|webcam|sexy?|tube|cam|p[o]rn|adult|com|net|org|online|ru|co|info|link)")]
    private static partial Regex BannedUrlsRegex();
    
    private static void ConfigurePostEndpoints()
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

        app.MapPost("/posts/upload", async ([FromBody] PostUploadRequest submission, HttpContext context, DatabaseContext database) =>
        {
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

            var postAccount = submission.AccountId is not null
                ? await database.Accounts.FindAsync(submission.AccountId)
                : null;
            if (postAccount is not null && submission.CanvasUser is not null)
            {
                return Results.BadRequest(
                    new ErrorResponse("Provided canvas user and account are mutually exclusive", "post.upload.exclusive"));
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
            
            // Spam filtering
            newPost.Title = CensorBannedUrls(newPost.Title);
            newPost.Description = CensorBannedUrls(newPost.Description);
            
            // Automatic sensitive content detection - (Thanks to https://profanity.dev)
            if (await ProbablyHasProfanity(newPost.Title) || await ProbablyHasProfanity(newPost.Description))
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
        .UseMiddleware<RateLimiterMiddleware>(app, PostUploadEndpointRegex, TimeSpan.FromSeconds(config.PostLimitSeconds));

        app.MapGet("/posts/contents/{postContentId:int}", async (int postContentId, DatabaseContext database) =>
        {
            if (await database.PostContents.FindAsync(postContentId) is not { } postContent)
            {
                return Results.NotFound(new ErrorResponse("Specified post content could not be found",  "posts.content.contentNotFound"));
            }

            var contentPath = Path.Join(config.PostsFolder, "Content", postContent.ContentKey);
            if (!File.Exists(contentPath))
            {
                return Results.NotFound(new ErrorResponse("Specified post content could not be found",  "posts.content.contentNotFound"));
            }

            var stream = File.OpenRead(contentPath);
            return Results.File(stream, postContent.ContentType, postContent.ContentKey);
        });

        app.MapPost("/posts/{id:int}/contents", async (int id, [FromForm] PostContentRequest request, HttpContext context, DatabaseContext database) =>
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
            var contentPath = Path.Join(config.PostsFolder, "Content");
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
            if (IsContentBanned(memoryStream, request.File.ContentType, database))
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
        // TODO: Harden this if necessary, https://andrewlock.net/exploring-the-dotnet-8-preview-form-binding-in-minimal-apis
        .DisableAntiforgery();
    }

    private static bool IsContentBanned(Stream data, string contentType, DatabaseContext database)
    {
        const string logPrefix = "Could not check if content is banned:";
        
        switch (contentType)
        {
            // We need to compute hashes for every frame / every n frames and compare against the database
            case "image/gif":
            {
                // TODO: Handle this
                break;
            }
            // Compute all hashes and then compare against database
            case "image/jpeg" or "image/png" or "image/webp":
            {
                var perceptualHash = new PerceptualHash();
                var contentHash = perceptualHash.Hash(data);
                var applicableBannedContents = database.BlockedContents.Where(content =>
                    content.HashType == ContentHashType.Perceptual && content.FileType == ContentFileType.Image);
                foreach (var bannedContent in applicableBannedContents)
                {
                    if (ulong.TryParse(bannedContent.Hash, out var bannedHash) && 
                        CompareHash.Similarity(contentHash, bannedHash) > config.MinBannedContentPerceptualPercent)
                    {
                        return true;
                    }
                    logger.LogWarning("{logPrefix} Banned content {id}'s hash could not be parsed as ulong",
                        logPrefix, bannedContent.Id);
                }
                break;
            }
            default:
            {
                logger.LogError("{logPrefix} Content of type {contentType} not recognised", logPrefix, contentType);
                return false;
            }
        }

        return false;
    }

    private static async Task<bool> ProbablyHasProfanity(string text)
    {
        const string logPrefix = "Failed to query profanity API:";
        try
        {
            var content = JsonContent.Create(new { Message = text }, options: defaultJsonOptions);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var result = await httpClient.PostAsync("https://vector.profanity.dev", content);
            if (!result.IsSuccessStatusCode)
            {
                logger.LogError("{logPrefix} Status {statusCode} received", logPrefix, result.StatusCode);
                return false;
            }

            var profanityResponse = await result.Content.ReadFromJsonAsync<ProfanityResponse>();
            if (profanityResponse is null)
            {
                logger.LogError("{logPrefix} Deserialised profanity response {statusCode} received", logPrefix, result.StatusCode);
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

    private static string CensorBannedUrls(string text)
    {
        return BannedUrlsRegex().Replace(text, match => 
            {
                var url = match.Value.Replace("https://", "").Replace("http://", "").Split('/')[0];
                return config.PostContentAllowedDomains.Contains(url) ? match.Value : new string('*', match.Length);
            })
            .Trim();
    }
}
