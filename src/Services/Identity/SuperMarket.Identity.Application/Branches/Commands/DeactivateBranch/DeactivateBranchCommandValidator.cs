using FluentValidation;

namespace SuperMarket.Identity.Application.Branches.Commands.DeactivateBranch;

/// <summary>
/// Validator for DeactivateBranchCommand.
/// </summary>
public sealed class DeactivateBranchCommandValidator : AbstractValidator<DeactivateBranchCommand>
{
    public DeactivateBranchCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch ID is required.");
    }
}
