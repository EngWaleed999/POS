namespace SuperMarket.BuildingBlocks.Domain;


public interface ISoftDeletable
{

    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAt { get; set; }

    string? DeletedBy { get; set; }
}
