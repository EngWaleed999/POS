namespace SuperMarket.BuildingBlocks.Application;

public sealed class CursorPagedList<T, TCursor>
{
    // -------------------------------------------------------------------------
    // Keyset Payload & Cursor Metadata (Zero COUNT(*) Overhead)
    // -------------------------------------------------------------------------
    public IReadOnlyCollection<T> Items { get; }
    public TCursor? NextCursor { get; }
    public bool HasNextPage => NextCursor is not null;

    public CursorPagedList(IReadOnlyCollection<T> items, TCursor? nextCursor)
    {
        Items = items;
        NextCursor = nextCursor;
    }
}
