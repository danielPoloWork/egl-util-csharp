using System.Linq.Expressions;

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

    // A direct ISpecification implementation — not via the Specification base, which resets one ordering
    // when the other is set — that supplies BOTH a primary ascending and descending key: an ambiguous,
    // invalid state the evaluator must reject rather than silently honor one and drop the other (#88).
    private sealed class BothOrderingsSpecification : ISpecification<Product>
    {
        public Expression<Func<Product, bool>>? Criteria => null;
        public IReadOnlyList<Expression<Func<Product, object>>> Includes => [];
        public IReadOnlyList<string> IncludeStrings => [];
        public Expression<Func<Product, object>>? OrderBy => product => product.Price;
        public Expression<Func<Product, object>>? OrderByDescending => product => product.Name;
        public int Skip => 0;
        public int Take => 0;
        public bool IsPagingEnabled => false;
    }

    private sealed class ByPriceThenName : Specification<Product>
    {
        public ByPriceThenName()
        {
            ApplyOrderBy(product => product.Price);
            ApplyThenBy(product => product.Name);
        }
    }

    private sealed class ByPriceThenNameDescending : Specification<Product>
    {
        public ByPriceThenNameDescending()
        {
            ApplyOrderBy(product => product.Price);
            ApplyThenByDescending(product => product.Name);
        }
    }

    private static void SeedWithDuplicatePrices(CatalogContext context)
    {
        var category = new Category(Guid.NewGuid(), "cat");
        context.Categories.Add(category);
        // Three products tie on price 20, inserted in an order (mike, alpha, zulu) that matches neither
        // Name-ascending nor Name-descending — so dropping the ThenBy and falling back to the provider's
        // default (insertion) tie order fails both tie-breaker tests, not just one.
        context.Products.AddRange(
            new Product(Guid.NewGuid(), "mike", 20, category.Id),
            new Product(Guid.NewGuid(), "charlie", 10, category.Id),
            new Product(Guid.NewGuid(), "alpha", 20, category.Id),
            new Product(Guid.NewGuid(), "zulu", 20, category.Id));
        context.SaveChanges();
        context.ChangeTracker.Clear();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Applies_a_secondary_then_by_as_a_deterministic_tie_breaker(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        SeedWithDuplicatePrices(catalog.Context);

        List<Product> result = SpecificationEvaluator.GetQuery(catalog.Context.Products, new ByPriceThenName()).ToList();

        // Price asc, then Name asc breaks the price-20 tie deterministically: charlie(10), then alpha, mike, zulu.
        Assert.Equal(new[] { "charlie", "alpha", "mike", "zulu" }, result.Select(product => product.Name));
        Assert.Equal(new[] { 10, 20, 20, 20 }, result.Select(product => product.Price));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Applies_a_secondary_then_by_descending(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        SeedWithDuplicatePrices(catalog.Context);

        List<Product> result =
            SpecificationEvaluator.GetQuery(catalog.Context.Products, new ByPriceThenNameDescending()).ToList();

        // Price asc, then Name desc: the price-20 tie orders zulu, mike, alpha.
        Assert.Equal(new[] { "charlie", "zulu", "mike", "alpha" }, result.Select(product => product.Name));
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

    [Fact]
    public void Supplying_both_orderings_is_rejected()
    {
        // #88: OrderBy and OrderByDescending are mutually exclusive. The evaluator used to apply OrderBy and
        // silently drop OrderByDescending; a direct ISpecification setting both now throws, mirroring the
        // paged-but-unordered guard, so the ambiguous state fails loudly instead of mis-ordering results.
        using var catalog = Catalog.Create(useSqlite: false);

        Assert.Throws<InvalidOperationException>(
            () => SpecificationEvaluator.GetQuery(catalog.Context.Products, new BothOrderingsSpecification()));
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
