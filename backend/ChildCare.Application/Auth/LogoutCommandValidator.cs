using FluentValidation;

namespace ChildCare.Application.Auth;

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("errors.auth.refresh_token_required");
    }
}
