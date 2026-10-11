using FluentValidation;

namespace SuperMarket.Identity.Application.Roles.Commands.CreateRole;

/// <summary>
/// Validator for CreateRoleCommand enforcing structural integrity and length boundaries.
/// Executed automatically via ValidationPipelineBehavior before reaching the handler.
/// </summary>
public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("Role name is required.")
            .MinimumLength(2).WithMessage("Role name must be at least 2 characters.")
            .MaximumLength(50).WithMessage("Role name cannot exceed 50 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description cannot exceed 255 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        When(x => x.PermissionGroupIds is not null && x.PermissionGroupIds.Count > 0, () =>
        {
            RuleFor(x => x.PermissionGroupIds)
                .Must(ids => ids!.Distinct().Count() == ids!.Count)
                .WithMessage("Permission group list cannot contain duplicate identifiers.");

            RuleForEach(x => x.PermissionGroupIds)
                .NotEmpty().WithMessage("Permission group identifier cannot be empty.");
        });
    }
}
