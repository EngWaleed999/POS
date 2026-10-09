using FluentValidation;

namespace SuperMarket.Identity.Application.Branches.Commands.DeleteBranch;

/// <summary>
/// Validator for DeleteBranchCommand.
/// </summary>
public sealed class DeleteBranchCommandValidator : AbstractValidator<DeleteBranchCommand>
{
    public DeleteBranchCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch ID is required.");
    }
}
