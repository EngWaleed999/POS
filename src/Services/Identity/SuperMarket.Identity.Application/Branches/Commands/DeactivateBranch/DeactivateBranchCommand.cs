using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Commands.DeactivateBranch;

/// <summary>
/// Command to temporarily suspend operations for a physical retail branch (e.g. renovation or seasonal closure).
/// </summary>
public sealed record DeactivateBranchCommand(Guid BranchId) : ICommand;
