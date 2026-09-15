namespace SuperMarket.BuildingBlocks.Application;

public sealed class PerformanceSettings
{
    // -------------------------------------------------------------------------
    // Configuration Section Name
    // -------------------------------------------------------------------------
    public const string SectionName = "Performance";

    // -------------------------------------------------------------------------
    // Configurable Properties (With Sensible Production Defaults)
    // -------------------------------------------------------------------------
    public int SlowRequestThresholdMs { get; init; } = 500;
}
