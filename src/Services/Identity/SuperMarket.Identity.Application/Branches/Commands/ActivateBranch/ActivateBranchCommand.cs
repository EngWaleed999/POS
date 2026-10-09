using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Commands.ActivateBranch;

/// <summary>
/// Command to restore operational activity for a previously deactivated retail branch.
/// </summary>
public sealed record ActivateBranchCommand(Guid BranchId) : ICommand;
