using AuthOfficial.ApiModel;
using FluentValidation;

namespace AuthOfficial.Validation;

public class ProfileUpdateRequestValidator : AbstractValidator<ProfileUpdateRequest>
{
    public ProfileUpdateRequestValidator()
    {
        RuleFor(x => x.DiscordHandle)
            .Matches(@"^(?=.{2,32}$)(?!(?:everyone|here)$)\.?[a-z0-9_]+(?:\.[a-z0-9_]+)*\.?$")
            .WithMessage("Invalid Discord handle format.")
            .MaximumLength(30);

        RuleFor(x => x.TwitterHandle)
            .Matches(@"^[\w@]+$")
            .WithMessage("Invalid Twitter handle format.")
            .MaximumLength(15);

        RuleFor(x => x.RedditHandle)
            .Matches(@"^[\w-]+$")
            .WithMessage("Invalid Reddit handle format.")
            .MaximumLength(20);
        
        RuleFor(x => x.Biography)
            .MinimumLength(1)
            .MaximumLength(360);
    }
}