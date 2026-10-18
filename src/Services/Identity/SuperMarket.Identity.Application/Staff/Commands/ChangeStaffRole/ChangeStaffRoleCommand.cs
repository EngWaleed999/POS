// Command to update a staff member's assigned job role.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Commands.ChangeStaffRole;

public sealed record ChangeStaffRoleCommand(Guid UserId, Guid NewRoleId) : ICommand;
