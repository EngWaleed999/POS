// Command to reassign a staff member to a new retail branch or head office.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Commands.TransferStaff;

public sealed record TransferStaffCommand(Guid UserId, Guid? NewBranchId) : ICommand;
