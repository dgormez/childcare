using System.Text.RegularExpressions;
using FluentValidation;

namespace ChildCare.Application.Children;

public class SetChildNrnCommandValidator : AbstractValidator<SetChildNrnCommand>
{
    public SetChildNrnCommandValidator()
    {
        RuleFor(x => x.Nrn)
            .Must(nrn => Regex.IsMatch(Normalize(nrn), @"^\d{11}$"))
            .WithMessage("errors.child.nrn_invalid_format")
            .DependentRules(() =>
            {
                RuleFor(x => x.Nrn)
                    .Must(nrn => RijksregisternummerChecksum.IsValid(Normalize(nrn)))
                    .WithMessage("errors.child.nrn_invalid_checksum");
            });
    }

    private static string Normalize(string? nrn) => Regex.Replace(nrn ?? string.Empty, @"[.\-\s]", string.Empty);
}
