using System.Text.RegularExpressions;
using FluentValidation;

namespace ChildCare.Application.Children;

// research.md R4: structural check only (11 digits after stripping dot/dash separators) — no
// day/month range check, no modulo-97 checksum. spec.md Assumptions explains why.
public class SetChildNrnCommandValidator : AbstractValidator<SetChildNrnCommand>
{
    public SetChildNrnCommandValidator()
    {
        RuleFor(x => x.Nrn)
            .Must(nrn => Regex.IsMatch(Regex.Replace(nrn ?? string.Empty, @"[.\-\s]", string.Empty), @"^\d{11}$"))
            .WithMessage("errors.child.nrn_invalid_format");
    }
}
