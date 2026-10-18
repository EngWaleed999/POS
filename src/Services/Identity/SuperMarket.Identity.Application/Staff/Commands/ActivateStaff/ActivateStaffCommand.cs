// Command to restore an active status for a deactivated staff member account.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Commands.ActivateStaff;

public sealed record ActivateStaffCommand(Guid UserId) : ICommand;
