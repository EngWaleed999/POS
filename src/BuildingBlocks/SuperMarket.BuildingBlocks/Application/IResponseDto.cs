namespace SuperMarket.BuildingBlocks.Application;

/// <summary>
/// Marker contract for read-side response DTOs.
/// Query contracts (IDtoQuery, IPagedQuery, ICursorPagedQuery) constrain their response type to this marker,
/// so the compiler rejects returning domain entities or aggregates from queries.
/// Domain entities must NEVER implement this interface.
/// </summary>
public interface IResponseDto
{
}
