namespace SuperMarket.BuildingBlocks.Application;

public record CursorParams<TCursor>
{
    // -------------------------------------------------------------------------
    // Defensive Fallback Configuration Limits
    // -------------------------------------------------------------------------
    public const int FallbackMaxPageSize = 100;
    public const int FallbackDefaultPageSize = 20;

    // -------------------------------------------------------------------------
    // Keyset Pagination Parameters
    // -------------------------------------------------------------------------
    public TCursor? Cursor { get; init; }
    public int PageSize { get; init; } = FallbackDefaultPageSize;

    public CursorParams()
    {
    }

    public CursorParams(TCursor? cursor, int pageSize)
    {
        Cursor = cursor;
        PageSize = Math.Clamp(pageSize, 1, FallbackMaxPageSize);
    }

    public CursorParams(TCursor? cursor, int pageSize, PaginationSettings settings)
    {
        Cursor = cursor;
        PageSize = Math.Clamp(pageSize, 1, settings.MaxPageSize);
    }
}
