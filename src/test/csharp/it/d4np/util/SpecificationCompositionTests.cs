using System.Linq.Expressions;

using It.D4np.Util.Application;
using It.D4np.Util.Infrastructure;

namespace It.D4np.Util.Tests;

public class SpecificationCompositionTests
{
    private sealed class PriceAtLeastSpec(int min) : Specification<Product>(product => product.Price >= min);

    private sealed class NameContainsSpec(string term) : Specification<Product>(product => product.Name.Contains(term));

    // A specification with no filter criteria (Criteria == null → "match all").
    private sealed class UnfilteredSpec : Specification<Product>;

    // Factories returning the interface type: the And/Or/Not/IsSatisfiedBy default-interface members are
    // only callable through an ISpecification-typed reference, not the concrete class.
    private static ISpecification<Product> PriceAtLeast(int min) => new PriceAtLeastSpec(min);

    private static ISpecification<Product> NameContains(string term) => new NameContainsSpec(term);

    private static ISpecification<Product> Unfiltered() => new UnfilteredSpec();

    private static Product ProductNamed(string name, int price) => new(Guid.NewGuid(), name, price, Guid.NewGuid());

    private static void Seed(CatalogContext context)
    {
        // One real category so the Product→Category FK holds on SQLite (which enforces it; InMemory doesn't).
        var category = new Category(Guid.NewGuid(), "catalog");
        context.Categories.Add(category);
        context.Products.AddRange(
            new Product(Guid.NewGuid(), "cheap book", 10, category.Id),
            new Product(Guid.NewGuid(), "pricey book", 30, category.Id),
            new Product(Guid.NewGuid(), "toy", 15, category.Id),
            new Product(Guid.NewGuid(), "gadget", 40, category.Id));
        context.SaveChanges();
        context.ChangeTracker.Clear();
    }

    // --- IsSatisfiedBy (in-memory) ---

    [Fact]
    public void IsSatisfiedBy_evaluates_the_criteria_in_memory()
    {
        ISpecification<Product> spec = PriceAtLeast(20);

        Assert.True(spec.IsSatisfiedBy(ProductNamed("x", 30)));
        Assert.False(spec.IsSatisfiedBy(ProductNamed("x", 10)));
    }

    [Fact]
    public void IsSatisfiedBy_matches_everything_when_there_is_no_criteria()
    {
        Assert.True(Unfiltered().IsSatisfiedBy(ProductNamed("anything", 0)));
    }

    [Fact]
    public void And_is_satisfied_only_when_both_criteria_match()
    {
        ISpecification<Product> spec = PriceAtLeast(20).And(NameContains("book"));

        Assert.True(spec.IsSatisfiedBy(ProductNamed("pricey book", 30)));  // both
        Assert.False(spec.IsSatisfiedBy(ProductNamed("cheap book", 10)));  // fails price
        Assert.False(spec.IsSatisfiedBy(ProductNamed("gadget", 40)));      // fails name
    }

    [Fact]
    public void Or_is_satisfied_when_either_criterion_matches()
    {
        ISpecification<Product> spec = PriceAtLeast(20).Or(NameContains("book"));

        Assert.True(spec.IsSatisfiedBy(ProductNamed("cheap book", 10))); // name only
        Assert.True(spec.IsSatisfiedBy(ProductNamed("gadget", 40)));     // price only
        Assert.False(spec.IsSatisfiedBy(ProductNamed("toy", 15)));       // neither
    }

    [Fact]
    public void Not_negates_the_criteria()
    {
        ISpecification<Product> spec = PriceAtLeast(20).Not();

        Assert.True(spec.IsSatisfiedBy(ProductNamed("x", 10)));
        Assert.False(spec.IsSatisfiedBy(ProductNamed("x", 30)));
    }

    // --- null-Criteria identities ---

    [Fact]
    public void And_with_an_unfiltered_spec_yields_the_other_criteria()
    {
        ISpecification<Product> spec = Unfiltered().And(PriceAtLeast(20));

        Assert.True(spec.IsSatisfiedBy(ProductNamed("x", 30)));
        Assert.False(spec.IsSatisfiedBy(ProductNamed("x", 10))); // the price filter still applies
    }

    [Fact]
    public void Or_with_an_unfiltered_spec_matches_everything()
    {
        ISpecification<Product> spec = PriceAtLeast(20).Or(Unfiltered());

        Assert.True(spec.IsSatisfiedBy(ProductNamed("x", 10))); // match-all wins
    }

    [Fact]
    public void Not_of_an_unfiltered_spec_matches_nothing()
    {
        ISpecification<Product> spec = Unfiltered().Not();

        Assert.False(spec.IsSatisfiedBy(ProductNamed("x", 30)));
    }

    // --- the combinators produce a pure predicate spec (no includes/ordering/paging) ---

    [Fact]
    public void A_combined_spec_carries_no_includes_ordering_or_paging()
    {
        ISpecification<Product> spec = PriceAtLeast(20).And(NameContains("book"));

        Assert.Empty(spec.Includes);
        Assert.Empty(spec.IncludeStrings);
        Assert.Null(spec.OrderBy);
        Assert.Null(spec.OrderByDescending);
        Assert.False(spec.IsPagingEnabled);
    }

    [Fact]
    public void Combined_criteria_is_a_flat_tree_with_no_nested_invocation()
    {
        // The combinators rebind onto a single parameter rather than composing via Expression.Invoke, so
        // the resulting Criteria contains no InvocationExpression. This keeps it translatable across EF
        // providers/versions (some cannot inline Invoke) — guards against regressing the rebinding.
        ISpecification<Product> spec = PriceAtLeast(20).And(NameContains("book"));

        Assert.NotNull(spec.Criteria);
        Assert.False(new InvocationFinder().ContainsInvocation(spec.Criteria!));
    }

    private sealed class InvocationFinder : ExpressionVisitor
    {
        private bool _found;

        public bool ContainsInvocation(Expression expression)
        {
            _found = false;
            Visit(expression);
            return _found;
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            _found = true;
            return base.VisitInvocation(node);
        }
    }

    // --- guards ---

    [Fact]
    public void And_and_Or_reject_a_null_other()
    {
        ISpecification<Product> spec = PriceAtLeast(20);

        Assert.Throws<ArgumentNullException>(() => spec.And(null!));
        Assert.Throws<ArgumentNullException>(() => spec.Or(null!));
    }

    [Fact]
    public void IsSatisfiedBy_rejects_a_null_entity()
    {
        ISpecification<Product> spec = PriceAtLeast(20);

        Assert.Throws<ArgumentNullException>(() => spec.IsSatisfiedBy(null!));
    }

    // --- EF-translatability: the combined criteria must run as SQL (a naive Expression.Invoke would throw) ---

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void And_composes_an_EF_translatable_query(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        Seed(catalog.Context);

        ISpecification<Product> spec = PriceAtLeast(20).And(NameContains("book"));
        List<string> names = SpecificationEvaluator.GetQuery(catalog.Context.Products, spec)
            .Select(product => product.Name).OrderBy(name => name).ToList();

        Assert.Equal(["pricey book"], names); // price >= 20 AND name ~ "book"
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Or_composes_an_EF_translatable_query(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        Seed(catalog.Context);

        ISpecification<Product> spec = PriceAtLeast(20).Or(NameContains("book"));
        List<string> names = SpecificationEvaluator.GetQuery(catalog.Context.Products, spec)
            .Select(product => product.Name).OrderBy(name => name).ToList();

        // price >= 20 OR name ~ "book": cheap book (name), pricey book (both), gadget (price).
        Assert.Equal(["cheap book", "gadget", "pricey book"], names);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Not_composes_an_EF_translatable_query(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        Seed(catalog.Context);

        ISpecification<Product> spec = PriceAtLeast(20).Not();
        List<string> names = SpecificationEvaluator.GetQuery(catalog.Context.Products, spec)
            .Select(product => product.Name).OrderBy(name => name).ToList();

        Assert.Equal(["cheap book", "toy"], names); // NOT (price >= 20)
    }
}
