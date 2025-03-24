using System.Security.Claims;
using AuthOfficial.DataModel;
using Microsoft.AspNetCore.Authorization;

namespace AuthOfficial.Authorization;

public class PostAuthorRequirement : IAuthorizationRequirement
{
}

public class PostAuthorizationHandler : AuthorizationHandler<PostAuthorRequirement, Post>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PostAuthorRequirement requirement, Post post)
    {
        /*var user = context.User;
        var authId = user.Claims.FindFirstAs<int>(ClaimTypes.NameIdentifier);
        var authType = user.Claims.FindFirstAs<AuthType>("type");

        if (authId != post.AuthorId || authType != post.Author.AuthType)
        {
            context.Fail(requirement);
        }
        {
            
        }
        if (authType == AuthType.Account && authId == post.AuthorId)
        {
            context.Succeed(requirement);
        }
        else if (authType == AuthType.CanvasUser && authId == post.CanvasUserAuthorId)
        {
            context.Succeed(requirement);
        }*/

        return Task.CompletedTask;
    }
}
