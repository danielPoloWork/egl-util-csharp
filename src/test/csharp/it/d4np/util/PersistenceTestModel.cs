using It.D4np.Util.Domain;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace It.D4np.Util.Tests;

/// <summary>A simple related entity used to exercise specification includes.</summary>
internal sealed class Category : Entity<Guid>
{
    public Category(Guid id, string name)
        : base(id) => Name = name;

    // Used by EF Core to materialize the entity (sets mapped properties post-construction).
    private Category()
        : base(Guid.Empty) => Name = string.Empty;

    public string Name { get; private set; }
}

/// <summary>An aggregate root used to exercise the persistence patterns against EF Core.</summary>
internal sealed class Product : AggregateRoot<Guid>
{
    public Product(Guid id, string name, int price, Guid categoryId)
        : base(id)
    {
        Name = name;
        Price = price;
        CategoryId = categoryId;
    }

    // Used by EF Core to materialize the entity (sets mapped properties post-construction).
    private Product()
        : base(Guid.Empty) => Name = string.Empty;

    public string Name { get; private set; }

    public int Price { get; private set; }

    public Guid CategoryId { get; private set; }

    public Category? Category { get; private set; }

    public void RaiseTestEvent(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
}

internal sealed class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The Id key is inherited (get-only) from Entity<TId>; configure it explicitly.
        modelBuilder.Entity<Category>().HasKey(category => category.Id);
        modelBuilder.Entity<Product>().HasKey(product => product.Id);

        // Domain events are behavior, not persisted state.
        modelBuilder.Entity<Product>().Ignore(product => product.DomainEvents);
        modelBuilder.Entity<Product>()
            .HasOne(product => product.Category)
            .WithMany()
            .HasForeignKey(product => product.CategoryId);
    }
}

/// <summary>
/// Creates a seeded <see cref="CatalogContext"/> backed by either the EF Core InMemory provider or
/// a SQLite in-memory database, so the same tests run against both a non-relational and a
/// relational provider.
/// </summary>
internal sealed class Catalog : IDisposable
{
    private readonly SqliteConnection? _connection;

    private Catalog(CatalogContext context, SqliteConnection? connection)
    {
        Context = context;
        _connection = connection;
    }

    public CatalogContext Context { get; }

    public static Catalog Create(bool useSqlite)
    {
        SqliteConnection? connection = null;
        DbContextOptions<CatalogContext> options;

        if (useSqlite)
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            options = new DbContextOptionsBuilder<CatalogContext>().UseSqlite(connection).Options;
        }
        else
        {
            options = new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase("catalog-" + Guid.NewGuid())
                .Options;
        }

        var context = new CatalogContext(options);
        context.Database.EnsureCreated();
        return new Catalog(context, connection);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection?.Dispose();
    }
}
