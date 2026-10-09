using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Commands.DeleteBranch;

/// <summary>
/// Command to permanently close and soft-delete a supermarket branch.
/// Hard deletion is strictly prohibited in enterprise retail to preserve transactional integrity and audit trails.
/// </summary>
public sealed record DeleteBranchCommand(Guid BranchId, string? DeletedBy = null) : ICommand;
