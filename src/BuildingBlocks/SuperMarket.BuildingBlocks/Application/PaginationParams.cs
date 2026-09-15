namespace SuperMarket.BuildingBlocks.Application;

public record PaginationParams
{
    // -------------------------------------------------------------------------
    // Defensive Fallback Configuration Limits
    // -------------------------------------------------------------------------
    public const int FallbackMaxPageSize = 100;
    public const int FallbackDefaultPageSize = 10;

    // -------------------------------------------------------------------------
    // Query Parameters (With Defensive Guards Against DoS & Invalid Bounds)
    // -------------------------------------------------------------------------
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = FallbackDefaultPageSize;

    public PaginationParams()
    {
    }

    public PaginationParams(int pageNumber, int pageSize)
    {
        PageNumber = Math.Max(1, pageNumber);
        PageSize = Math.Clamp(pageSize, 1, FallbackMaxPageSize);
    }

    public PaginationParams(int pageNumber, int pageSize, PaginationSettings settings)
    {
        PageNumber = Math.Max(1, pageNumber);
        PageSize = Math.Clamp(pageSize, 1, settings.MaxPageSize);
    }
}
