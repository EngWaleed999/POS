using FluentValidation;

namespace SuperMarket.Identity.Application.Branches.Commands.ActivateBranch;

/// <summary>
/// Validator for ActivateBranchCommand.
/// </summary>
public sealed class ActivateBranchCommandValidator : AbstractValidator<ActivateBranchCommand>
{
    public ActivateBranchCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch ID is required.");
    }
}
