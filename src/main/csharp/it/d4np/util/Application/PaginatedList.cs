namespace It.D4np.Util.Application;

/// <summary>
/// An immutable page of results: the <see cref="Items"/> for the current page plus the paging
/// metadata (1-based <see cref="PageIndex"/>, <see cref="PageSize"/>, <see cref="TotalCount"/>, and
/// the derived <see cref="TotalPages"/>, <see cref="HasPrevious"/>, and <see cref="HasNext"/>).
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class PaginatedList<T>
{
    /// <summary>Creates a page of results.</summary>
    /// <param name="items">The items on this page; must not be <see langword="null"/>.</param>
    /// <param name="pageIndex">The 1-based index of this page; must be at least 1.</param>
    /// <param name="pageSize">The page size used for the query; must be at least 1.</param>
    /// <param name="totalCount">The total number of items across all pages; must not be negative.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageIndex"/> or <paramref name="pageSize"/> is less than 1, or
    /// <paramref name="totalCount"/> is negative.
    /// </exception>
    public PaginatedList(IReadOnlyList<T> items, int pageIndex, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageIndex, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        Items = items;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    /// <summary>The items on the current page.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>The 1-based index of the current page.</summary>
    public int PageIndex { get; }

    /// <summary>The page size used to compute the paging.</summary>
    public int PageSize { get; }

    /// <summary>The total number of items across all pages.</summary>
    public int TotalCount { get; }

    /// <summary>The total number of pages, derived from <see cref="TotalCount"/> and <see cref="PageSize"/>.</summary>
    public int TotalPages { get; }

    /// <summary>Whether a page precedes the current one.</summary>
    public bool HasPrevious => PageIndex > 1;

    /// <summary>Whether a page follows the current one.</summary>
    public bool HasNext => PageIndex < TotalPages;
}
