using It.D4np.Util.Application;
using It.D4np.Util.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace It.D4np.Util.Tests;

public class SpecificationEvaluatorTests
{
    private sealed class InCategoryByPriceDescending : Specification<Product>
    {
        public InCategoryByPriceDescending(Guid categoryId)
            : base(product => product.CategoryId == categoryId)
        {
            AddInclude(product => product.Category!);
            ApplyOrderByDescending(product => product.Price);
        }
    }

    private sealed class PagedByPriceAscending : Specification<Product>
    {
        public PagedByPriceAscending(int skip, int take)
        {
            ApplyOrderBy(product => product.Price);
            ApplyPaging(skip, take);
        }
    }

    private sealed class InCategoryViaIncludeString : Specification<Product>
    {
        public InCategoryViaIncludeString(Guid categoryId)
            : base(product => product.CategoryId == categoryId)
        {
            AddInclude("Category"); // string-based include path
            ApplyOrderBy(product => product.Price);
        }
    }

    private sealed class UnorderedPaged : Specification<Product>
    {
        public UnorderedPaged() => ApplyPaging(skip: 0, take: 2); // paging with no ordering
    }

    private static Category SeedCatalog(CatalogContext context)
    {
        var books = new Category(Guid.NewGuid(), "books");
        var toys = new Category(Guid.NewGuid(), "toys");
        context.Categories.AddRange(books, toys);
        context.Products.AddRange(
            new Product(Guid.NewGuid(), "cheap book", 10, books.Id),
            new Product(Guid.NewGuid(), "pricey book", 30, books.Id),
            new Product(Guid.NewGuid(), "mid book", 20, books.Id),
            new Product(Guid.NewGuid(), "toy", 15, toys.Id));
        context.SaveChanges();
        context.ChangeTracker.Clear(); // force queries to materialize from the store
        return books;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Applies_criteria_include_and_descending_order(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        Category books = SeedCatalog(catalog.Context);

        var specification = new InCategoryByPriceDescending(books.Id);
        List<Product> result = SpecificationEvaluator.GetQuery(catalog.Context.Products, specification).ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, product => Assert.Equal(books.Id, product.CategoryId));
        Assert.All(result, product => Assert.NotNull(product.Category)); // include eager-loaded the navigation
        Assert.Equal(new[] { 30, 20, 10 }, result.Select(product => product.Price));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Applies_paging(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        SeedCatalog(catalog.Context);

        var specification = new PagedByPriceAscending(skip: 1, take: 2);
        List<Product> result = SpecificationEvaluator.GetQuery(catalog.Context.Products, specification).ToList();

        // ascending prices are 10, 15, 20, 30; skip 1 then take 2 -> 15, 20.
        Assert.Equal(new[] { 15, 20 }, result.Select(product => product.Price));
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        using var catalog = Catalog.Create(useSqlite: false);

        Assert.Throws<ArgumentNullException>(
            () => SpecificationEvaluator.GetQuery<Product>(null!, new PagedByPriceAscending(0, 1)));
        Assert.Throws<ArgumentNullException>(
            () => SpecificationEvaluator.GetQuery(catalog.Context.Products, null!));
    }

    [Fact]
    public void Paging_without_an_ordering_is_rejected()
    {
        using var catalog = Catalog.Create(useSqlite: false);

        Assert.Throws<InvalidOperationException>(
            () => SpecificationEvaluator.GetQuery(catalog.Context.Products, new UnorderedPaged()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Applies_string_based_includes(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        Category books = SeedCatalog(catalog.Context);

        var specification = new InCategoryViaIncludeString(books.Id);
        List<Product> result = SpecificationEvaluator.GetQuery(catalog.Context.Products, specification).ToList();

        Assert.NotEmpty(result);
        Assert.All(result, product => Assert.NotNull(product.Category));
    }
}
