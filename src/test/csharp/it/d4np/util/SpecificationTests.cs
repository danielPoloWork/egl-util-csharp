using It.D4np.Util.Application;

namespace It.D4np.Util.Tests;

public class SpecificationTests
{
    private sealed class Widget
    {
        public int Size { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    private sealed class EmptySpec : Specification<Widget>;

    private sealed class BigWidgets : Specification<Widget>
    {
        public BigWidgets()
            : base(widget => widget.Size > 10)
        {
            AddInclude(widget => widget.Name);
            ApplyOrderBy(widget => widget.Size);
            ApplyPaging(skip: 5, take: 10);
        }
    }

    private sealed class FlipsOrdering : Specification<Widget>
    {
        public FlipsOrdering()
        {
            ApplyOrderByDescending(widget => widget.Size);
            ApplyOrderBy(widget => widget.Name);
        }
    }

    private sealed class NegativePaging : Specification<Widget>
    {
        public NegativePaging() => ApplyPaging(skip: -1, take: 10);
    }

    [Fact]
    public void Captures_criteria_includes_ordering_and_paging()
    {
        var spec = new BigWidgets();

        Assert.NotNull(spec.Criteria);
        Assert.Single(spec.Includes);
        Assert.NotNull(spec.OrderBy);
        Assert.Null(spec.OrderByDescending);
        Assert.True(spec.IsPagingEnabled);
        Assert.Equal(5, spec.Skip);
        Assert.Equal(10, spec.Take);
    }

    [Fact]
    public void A_default_specification_has_no_criteria_or_paging()
    {
        var spec = new EmptySpec();

        Assert.Null(spec.Criteria);
        Assert.Empty(spec.Includes);
        Assert.False(spec.IsPagingEnabled);
        Assert.Null(spec.OrderBy);
        Assert.Null(spec.OrderByDescending);
    }

    [Fact]
    public void Ascending_and_descending_ordering_are_mutually_exclusive()
    {
        var spec = new FlipsOrdering();

        Assert.NotNull(spec.OrderBy);
        Assert.Null(spec.OrderByDescending);
    }

    [Fact]
    public void Negative_paging_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NegativePaging());
    }
}
