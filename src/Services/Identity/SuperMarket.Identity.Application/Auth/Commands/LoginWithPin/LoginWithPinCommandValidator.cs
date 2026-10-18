// Validator for LoginWithPinCommand ensuring structural validity of cashier credentials and register ID.

using FluentValidation;

namespace SuperMarket.Identity.Application.Auth.Commands.LoginWithPin;

public sealed class LoginWithPinCommandValidator : AbstractValidator<LoginWithPinCommand>
{
    public LoginWithPinCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.");

        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Matches(@"^\d{4,6}$").WithMessage("PIN must be between 4 and 6 numeric digits.");

        RuleFor(x => x.RegisterId)
            .NotEmpty().WithMessage("A valid non-empty POS register ID is required.");
    }
}
