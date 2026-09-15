namespace SuperMarket.BuildingBlocks.Application;

public sealed class PaginationSettings
{
    // -------------------------------------------------------------------------
    // Configuration Section Name
    // -------------------------------------------------------------------------
    public const string SectionName = "Pagination";

    // -------------------------------------------------------------------------
    // Configurable Properties (With Sensible Production Defaults)
    // -------------------------------------------------------------------------
    public int DefaultPageSize { get; init; } = 10;
    public int MaxPageSize { get; init; } = 100;
}
