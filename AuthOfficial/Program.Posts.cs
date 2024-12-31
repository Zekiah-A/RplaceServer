using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AuthOfficial.ApiModel;
using AuthOfficial.Configuration;
using AuthOfficial.DataModel;
using AuthOfficial.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthOfficial;

internal static partial class Program
{    
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
                query = query.Where(post => post.AccountAuthorId == authorId);
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

        app.MapPatch("/posts/{id:int}", async (int id, IValidator<PostUpdateRequest> validator, PostUpdateRequest request, HttpContext context, CensorService censor, IAuthorizationService authorizationService, DatabaseContext database) =>
        {
            if (await database.Posts.FindAsync(id) is not { } post)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Specified post does not exist", "posts.notFound"));
                return;
            }

            // Determine if user has governing rights over post
            var result = await authorizationService.AuthorizeAsync(context.User, id, "PostAuthorPolicy");
            if (!result.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("You are forbidden from editing this post", "posts.update.forbidden"));
                return;
            }

            // Validate new submission
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    "Specified post details were invalid", 
                    "posts.update.invalidDetails",
                    validationResult.ToDictionary()));
                return;
            }

            if (request.Title is not null)
            {
                post.Title = censor.CensorBanned(request.Title);
            }
            if (request.Description is not null)
            {
                post.Description = censor.CensorBanned(request.Description);
            }
            post.LastEdited = DateTime.UtcNow;
            await database.SaveChangesAsync();

            context.Response.StatusCode = StatusCodes.Status201Created;
            return;
        })
        .RequireAuthType(AuthType.Account | AuthType.CanvasUser)
        .RequireClaims(ClaimTypes.NameIdentifier);


        app.MapDelete("/posts/{id:int}", async (int id, HttpContext context, IAuthorizationService authorizationService, DatabaseContext database) =>
        {
            if (await database.Posts.FindAsync(id) is not { } post)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Specified post does not exist", "posts.notFound"));
                return;
            }

            // Determine if user has governing rights over post
            var result = await authorizationService.AuthorizeAsync(context.User, id, "PostAuthorPolicy");
            if (!result.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("You are forbidden from deleting this post", "posts.delete.forbidden"));
                return;
            }

            // TODO: Delete post
            throw new NotImplementedException("Post deletion is not yet implemented!");
        })
        .RequireAuthType(AuthType.Account | AuthType.CanvasUser)
        .RequireClaims(ClaimTypes.NameIdentifier);

        app.MapPost("/posts", async (IValidator<PostUploadRequest> validator, PostUploadRequest submission, HttpContext context, CensorService censor, IOptionsSnapshot<PostsConfiguration> config, DatabaseContext database) =>
        {
            var user = context.User;
            var authId = user.Claims.FindFirstAs<int>(ClaimTypes.NameIdentifier);
            var authType = user.Claims.FindFirstAs<AuthType>("type");

            var validationResult = await validator.ValidateAsync(submission);
            if (!validationResult.IsValid)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    "Specified post details were invalid", 
                    "posts.create.invalidDetails",
                    validationResult.ToDictionary()));
                return;
            }

            var newPost = new Post(submission.Title, submission.Description)
            {
                Upvotes = 0,
                Downvotes = 0,
                CreationDate = DateTime.Now.ToUniversalTime(),
            };

            if (authType == AuthType.Account)
            {
                newPost.AccountAuthorId = authId;
            }
            else if (authType == AuthType.CanvasUser)
            {
                newPost.CanvasUserAuthorId = authId;
            }

            // Spam filtering
            newPost.Title = censor.CensorBanned(newPost.Title);
            newPost.Description = censor.CensorBanned(newPost.Description);
            
            // Automatic sensitive content detection - (Thanks to https://profanity.dev)
            if (await censor.ProbablyHasProfanity(newPost.Title) || await censor.ProbablyHasProfanity(newPost.Description))
            {
                newPost.HasSensitiveContent = true;
            }

            await database.Posts.AddAsync(newPost);
            await database.SaveChangesAsync();

            context.Response.StatusCode = StatusCodes.Status201Created;
            await context.Response.WriteAsJsonAsync(
                new PostCreateResponse(newPost.Id));
            return;
        })
        .RequireAuthorization()
        //.RateLimit(TimeSpan.FromSeconds(config.PostLimitSeconds))
        .RequireAuthType(AuthType.Account | AuthType.CanvasUser)
        .RequireClaims(ClaimTypes.NameIdentifier);

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

        app.MapPost("/posts/{id:int}/contents", async (int id, [FromForm] PostContentRequest request, HttpContext context, IAuthorizationService authorizationService, CensorService censor, IOptionsSnapshot<PostsConfiguration> config, DatabaseContext database) =>
        {
            var pendingPost = await database.Posts.Include(post => post.Contents).FirstOrDefaultAsync(post => post.Id == id);
            if (await database.Posts.FindAsync(id) is not { } post)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Specified post does not exist", "posts.notFound"));
                return;
            }

            var user = context.User;
            var authId = user.Claims.FindFirstAs<int>(ClaimTypes.NameIdentifier);
            var authType = user.Claims.FindFirstAs<AuthType>("type");

            // Determine if user has governing rights over post
            var result = await authorizationService.AuthorizeAsync(context.User, id, "PostAuthorPolicy");
            if (!result.Succeeded)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("You are forbidden from uploading content to this post", "posts.content.upload.forbidden"));
                return;
            }

            // Limit stream length to 5MB to prevent excessively large uploads
            if (context.Request.ContentLength > 5_000_000)
            {
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Provided content length was larger than maximum allowed size (5mb)", "posts.content.upload.tooLarge"));
                return;
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
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("File was not of valid type 'image/gif, image/jpeg, image/png, image/webp'", "post.content.upload.invalidType"));
                return;
            }

            // Explicitly ensure that contents are fetched from navigation property
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
                var address = context.Connection.RemoteIpAddress;
                logger.LogInformation(
                    "Client {authType} {authId} ({address}) blocked from uploading content for post {id} to {contentKey}, banned content hash match detected",
                    authType, authId, address, id, contentKey);

                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Provided file was not valid", "post.content.upload.invalidFile"));
                return;
            }

            // Content filters
            try
            {
                var nudeNetResult = await nudeNetAiService.RunModel(memoryStream.ToArray());
                if (nudeNetResult is null)
                {
                    logger.LogWarning("Couuldn't run CensorCore model on post content {contentKey}: result was null",
                        contentKey);
                }
                else
                {
                    if (nudeNetResult.Results.Count > 0)
                    {
                        pendingPost.HasSensitiveContent = true;
                    }

                    var session = nudeNetResult.Session;
                    if (session is not null)
                    {
                        logger.LogTrace("Ran CensorCore on post content {contentKey}, {Image} {Tensor} {Model}",
                            contentKey, session.ImageLoadTime, session.TensorLoadTime, session.ModelRunTime);
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

            // Store new content in database
            var postContent = new PostContent(contentKey, request.File.ContentType, id);
            await database.PostContents.AddAsync(postContent);
            await database.SaveChangesAsync();
            
            context.Response.StatusCode = StatusCodes.Status200OK;
            return;
        })
        .RequireAuthorization()
        .RequireAuthType(AuthType.Account | AuthType.CanvasUser)
        // TODO: Harden this if necessary, https://andrewlock.net/exploring-the-dotnet-8-preview-form-binding-in-minimal-apis
        .DisableAntiforgery();
    }
}
