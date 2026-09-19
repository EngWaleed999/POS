using FluentAssertions;
using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.BuildingBlocks.UnitTests.Application;

/// <summary>
/// Unit tests for Pagination primitives (<see cref="PagedList{T}"/>, <see cref="PaginationParams"/>,
/// and <see cref="CursorPagedList{T, TCursor}"/>) validating page math, boundary clamping, and navigation flags.
/// </summary>
public class PaginationTests
{
    // =========================================================================
    // 1. TotalPages Mathematical Calculation ([Theory] with [InlineData])
    // =========================================================================

    [Theory]
    [InlineData(100, 10, 10)]
    [InlineData(101, 10, 11)]
    [InlineData(99, 10, 10)]
    [InlineData(0, 10, 0)]
    [InlineData(5, 10, 1)]
    [InlineData(50, 25, 2)]
    public void TotalPages_ShouldCalculateCorrectly_BasedOnTotalCountAndPageSize(
        int totalCount, int pageSize, int expectedTotalPages)
    {
        // Arrange & Act
        var pagedList = new PagedList<string>(Array.Empty<string>(), pageNumber: 1, pageSize: pageSize, totalCount: totalCount);

        // Assert
        pagedList.TotalPages.Should().Be(expectedTotalPages);
    }

    // =========================================================================
    // 2. Navigation Flags (HasPreviousPage & HasNextPage)
    // =========================================================================

    [Theory]
    [InlineData(1, 100, 10, false, true)]   // First page of 10
    [InlineData(5, 100, 10, true, true)]    // Middle page (5 of 10)
    [InlineData(10, 100, 10, true, false)]  // Last page of 10
    [InlineData(1, 5, 10, false, false)]    // Single page (1 of 1)
    [InlineData(1, 0, 10, false, false)]    // Empty collection (0 items)
    public void NavigationFlags_ShouldEvaluateCorrectly_AtPageBoundaries(
        int pageNumber, int totalCount, int pageSize, bool expectedHasPrev, bool expectedHasNext)
    {
        // Arrange & Act
        var pagedList = new PagedList<int>(new[] { 1, 2, 3 }, pageNumber, pageSize, totalCount);

        // Assert
        pagedList.HasPreviousPage.Should().Be(expectedHasPrev);
        pagedList.HasNextPage.Should().Be(expectedHasNext);
    }

    // =========================================================================
    // 3. Defensive Parameter Clamping in PaginationParams ([Theory])
    // =========================================================================

    [Theory]
    [InlineData(0, 1)]       // 0 clamped to 1
    [InlineData(-5, 1)]      // Negative clamped to 1
    [InlineData(1, 1)]       // Valid preserved
    [InlineData(50, 50)]     // Valid preserved
    public void PaginationParams_ShouldClampPageNumber_ToMinimumOfOne(int inputPage, int expectedPage)
    {
        // Arrange & Act
        var parameters = new PaginationParams(inputPage, 10);

        // Assert
        parameters.PageNumber.Should().Be(expectedPage);
    }

    [Theory]
    [InlineData(0, 1)]       // 0 clamped to min 1
    [InlineData(-10, 1)]     // Negative clamped to min 1
    [InlineData(10, 10)]     // Normal preserved
    [InlineData(100, 100)]   // Max boundary preserved
    [InlineData(500, 100)]   // Exceeds max clamped to 100 (DoS defense)
    public void PaginationParams_ShouldClampPageSize_BetweenOneAndFallbackMax(int inputSize, int expectedSize)
    {
        // Arrange & Act
        var parameters = new PaginationParams(1, inputSize);

        // Assert
        parameters.PageSize.Should().Be(expectedSize);
    }

    [Fact]
    public void PaginationParams_WithCustomSettings_ShouldHonorConfiguredMaxPageSize()
    {
        // Arrange
        var settings = new PaginationSettings { MaxPageSize = 250 };

        // Act
        var parameters = new PaginationParams(pageNumber: 2, pageSize: 300, settings: settings);

        // Assert
        parameters.PageNumber.Should().Be(2);
        parameters.PageSize.Should().Be(250, "page size must be clamped to the custom configured max page size");
    }

    // =========================================================================
    // 4. Cursor Pagination (CursorPagedList & CursorParams)
    // =========================================================================

    [Fact]
    public void CursorPagedList_ShouldIndicateNextPage_WhenNextCursorIsPresent()
    {
        // Arrange & Act
        var pagedListWithNext = new CursorPagedList<string, string>(new[] { "Item1", "Item2" }, "cursor_token_xyz");
        var pagedListLastPage = new CursorPagedList<string, string>(new[] { "Item3" }, null);

        // Assert
        pagedListWithNext.HasNextPage.Should().BeTrue();
        pagedListWithNext.NextCursor.Should().Be("cursor_token_xyz");

        pagedListLastPage.HasNextPage.Should().BeFalse();
        pagedListLastPage.NextCursor.Should().BeNull();
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(1000, 100)]
    public void CursorParams_ShouldClampPageSize_ToSafeBounds(int requestedPageSize, int expectedPageSize)
    {
        // Arrange & Act
        var cursorParams = new CursorParams<long>(cursor: 12345L, pageSize: requestedPageSize);

        // Assert
        cursorParams.Cursor.Should().Be(12345L);
        cursorParams.PageSize.Should().Be(expectedPageSize);
    }
}
