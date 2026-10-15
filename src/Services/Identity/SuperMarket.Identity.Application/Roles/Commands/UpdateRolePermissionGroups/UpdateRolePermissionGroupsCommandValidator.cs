using FluentValidation;

namespace SuperMarket.Identity.Application.Roles.Commands.UpdateRolePermissionGroups;

/// <summary>
/// Validator for UpdateRolePermissionGroupsCommand.
/// Enforces defensive checks: non-empty RoleId, non-null list, non-empty Guids, and no duplicate IDs.
/// </summary>
public sealed class UpdateRolePermissionGroupsCommandValidator : AbstractValidator<UpdateRolePermissionGroupsCommand>
{
    public UpdateRolePermissionGroupsCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role identifier is required.");

        RuleFor(x => x.PermissionGroupIds)
            .NotNull().WithMessage("Permission groups collection cannot be null.")
            .Must(ids => ids == null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Permission groups list cannot contain duplicate identifiers.");

        RuleForEach(x => x.PermissionGroupIds)
            .NotEmpty().WithMessage("Permission group identifier cannot be empty.");
    }
}
