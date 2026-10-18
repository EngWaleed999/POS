// Validator for SetStaffPinCommand enforcing PIN entropy and security invariants.

using FluentValidation;

namespace SuperMarket.Identity.Application.Staff.Commands.SetStaffPin;

public sealed class SetStaffPinCommandValidator : AbstractValidator<SetStaffPinCommand>
{
    public SetStaffPinCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("A valid non-empty user ID is required.");

        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("PIN is required.")
            .Matches(@"^\d{4,6}$").WithMessage("PIN must be between 4 and 6 numeric digits.")
            .Must(NotBeAllIdenticalDigits).WithMessage("PIN cannot consist of identical repeating digits.")
            .Must(NotBeSequentialDigits).WithMessage("PIN cannot consist of consecutive ascending or descending digits.");
    }

    private static bool NotBeAllIdenticalDigits(string pin)
    {
        if (string.IsNullOrEmpty(pin))
            return true;

        return pin.Distinct().Count() > 1;
    }

    private static bool NotBeSequentialDigits(string pin)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length < 2)
            return true;

        bool isAscending = true;
        bool isDescending = true;

        for (int i = 0; i < pin.Length - 1; i++)
        {
            if (pin[i + 1] - pin[i] != 1)
                isAscending = false;

            if (pin[i] - pin[i + 1] != 1)
                isDescending = false;
        }

        return !isAscending && !isDescending;
    }
}
