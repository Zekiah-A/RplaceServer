using AuthOfficial.ApiModel;
using AuthOfficial.DataModel;
using AuthOfficial.Services;
using FluentValidation;

namespace AuthOfficial.Validation;

public class PostUpdateRequestValidator : AbstractValidator<PostUpdateRequest>
{
    public PostUpdateRequestValidator(CensorService censor)
    {
        RuleFor(post => post.Title)
            .MinimumLength(1)
            .MaximumLength(64)
            .WithMessage("Post title should be between 1-64 characters long");

        RuleFor(post => post.Description)
            .MaximumLength(360)
            .WithMessage("Post description can not be longer than 360 characters");
    }
}