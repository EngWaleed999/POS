using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.BuildingBlocks.Infrastructure;

/// <summary>
/// Extension methods for EF Core <see cref="ModelBuilder"/> to automate domain conventions.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Configures an automatic Global Query Filter for all entities implementing <see cref="ISoftDeletable"/>,
    /// ensuring soft-deleted records are automatically excluded from queries unless explicitly bypassed using
    /// <c>IgnoreQueryFilters()</c>.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder being configured.</param>
    public static void ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var falseConstant = Expression.Constant(false);
                var filter = Expression.Lambda(Expression.Equal(property, falseConstant), parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
