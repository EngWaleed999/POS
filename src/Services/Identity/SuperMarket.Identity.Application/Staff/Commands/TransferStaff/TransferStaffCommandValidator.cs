// Validator for TransferStaffCommand enforcing user and optional branch format.

using FluentValidation;

namespace SuperMarket.Identity.Application.Staff.Commands.TransferStaff;

public sealed class TransferStaffCommandValidator : AbstractValidator<TransferStaffCommand>
{
    public TransferStaffCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("A valid non-empty user ID is required.");

        RuleFor(x => x.NewBranchId)
            .NotEmpty().WithMessage("New branch ID cannot be empty when specified.")
            .When(x => x.NewBranchId.HasValue);
    }
}
