// Validator for ChangeStaffRoleCommand enforcing valid identifiers.

using FluentValidation;

namespace SuperMarket.Identity.Application.Staff.Commands.ChangeStaffRole;

public sealed class ChangeStaffRoleCommandValidator : AbstractValidator<ChangeStaffRoleCommand>
{
    public ChangeStaffRoleCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("A valid non-empty user ID is required.");

        RuleFor(x => x.NewRoleId)
            .NotEmpty().WithMessage("A valid non-empty new role ID is required.");
    }
}
