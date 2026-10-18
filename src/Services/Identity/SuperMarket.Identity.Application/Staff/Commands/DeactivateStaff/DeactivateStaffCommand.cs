// Command to suspend a staff member account locally and revoke active sessions in Keycloak.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Commands.DeactivateStaff;

public sealed record DeactivateStaffCommand(Guid UserId) : ICommand;
