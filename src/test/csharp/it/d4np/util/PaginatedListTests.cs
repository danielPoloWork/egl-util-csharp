using It.D4np.Util.Application;

namespace It.D4np.Util.Tests;

public class PaginatedListTests
{
    [Fact]
    public void Computes_total_pages_and_navigation_for_a_middle_page()
    {
        var page = new PaginatedList<int>([4, 5, 6], pageIndex: 2, pageSize: 3, totalCount: 10);

        Assert.Equal(4, page.TotalPages); // ceil(10 / 3)
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
        Assert.Equal([4, 5, 6], page.Items);
    }

    [Fact]
    public void First_page_has_no_previous()
    {
        var page = new PaginatedList<int>([1, 2, 3], 1, 3, 10);

        Assert.False(page.HasPrevious);
        Assert.True(page.HasNext);
    }

    [Fact]
    public void Last_page_has_no_next()
    {
        var page = new PaginatedList<int>([10], 4, 3, 10);

        Assert.True(page.HasPrevious);
        Assert.False(page.HasNext);
    }

    [Fact]
    public void Empty_result_has_zero_pages_and_no_navigation()
    {
        var page = new PaginatedList<int>([], 1, 20, 0);

        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasPrevious);
        Assert.False(page.HasNext);
        Assert.Empty(page.Items);
    }

    [Fact]
    public void Overrun_page_index_beyond_the_last_page_has_a_previous_but_no_next()
    {
        // #56: pageIndex > TotalPages is accepted by the ctor; pin the navigation in that state.
        var page = new PaginatedList<int>([], pageIndex: 5, pageSize: 3, totalCount: 10);

        Assert.Equal(4, page.TotalPages); // ceil(10 / 3)
        Assert.True(page.HasPrevious); // pages precede an overrun page
        Assert.False(page.HasNext); // nothing follows the last page
    }

    [Fact]
    public void Last_page_on_an_exact_multiple_has_no_next()
    {
        // #56: exact last-page edge (totalCount an exact multiple of pageSize) — guards an off-by-one in
        // HasNext (PageIndex < TotalPages, not <=).
        var page = new PaginatedList<int>([7, 8, 9], pageIndex: 3, pageSize: 3, totalCount: 9);

        Assert.Equal(3, page.TotalPages); // ceil(9 / 3)
        Assert.True(page.HasPrevious);
        Assert.False(page.HasNext); // page 3 of 3 is the last
    }

    [Fact]
    public void Empty_result_beyond_the_first_page_has_a_previous_but_no_next()
    {
        // #56: totalCount == 0 (TotalPages == 0) with pageIndex > 1 — HasPrevious follows purely from
        // PageIndex, HasNext from PageIndex < TotalPages (0), so this pins the derived boundary.
        var page = new PaginatedList<int>([], pageIndex: 2, pageSize: 20, totalCount: 0);

        Assert.Equal(0, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.False(page.HasNext);
    }

    [Fact]
    public void Rejects_null_items()
    {
        Assert.Throws<ArgumentNullException>(() => new PaginatedList<int>(null!, 1, 10, 0));
    }

    [Theory]
    [InlineData(0, 10, 0)]  // pageIndex < 1
    [InlineData(1, 0, 0)]   // pageSize < 1
    [InlineData(1, 10, -1)] // totalCount < 0
    public void Rejects_out_of_range_arguments(int pageIndex, int pageSize, int totalCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PaginatedList<int>([], pageIndex, pageSize, totalCount));
    }
}
