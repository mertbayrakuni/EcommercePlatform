using CatalogService.Data;
using CatalogService.Infrastructure;
using CatalogService.Models;
using CatalogService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CatalogService.Tests;

public class OrderEventConsumerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CatalogDbContext _db;
    private readonly OrderEventConsumer _sut;

    public OrderEventConsumerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var opts = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new CatalogDbContext(opts);
        _db.Database.EnsureCreated();

        SeedData();

        var inventoryService = new InventoryService(_db);

        var provider = new Mock<IServiceProvider>();
        provider
            .Setup(p => p.GetService(typeof(IInventoryService)))
            .Returns(inventoryService);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory
            .Setup(f => f.CreateScope())
            .Returns(scope.Object);

        _sut = new OrderEventConsumer(
            scopeFactory.Object,
            Mock.Of<IConfiguration>(),
            NullLogger<OrderEventConsumer>.Instance);
    }

    // ── seed helpers ─────────────────────────────────────────────────────────

    private void SeedData()
    {
        var category = new Category { Name = "Electronics", Slug = "electronics" };
        _db.Categories.Add(category);
        _db.SaveChanges();

        _db.Products.AddRange(
            new Product { Id = 1, Name = "Widget A", Sku = "SKU-001", Price = 9.99m, Stock = 50, IsActive = true, CategoryId = category.Id },
            new Product { Id = 2, Name = "Widget B", Sku = "SKU-002", Price = 19.99m, Stock = 10, IsActive = true, CategoryId = category.Id }
        );
        _db.SaveChanges();
    }

    // ── HandleOrderCancelledAsync ─────────────────────────────────────────────

    [Fact]
    public async Task HandleOrderCancelledAsync_SingleItem_RestoresStock()
    {
        var msg = new OrderCancelledMessage(42, [new OrderItemMessage(1, 5)]);

        await _sut.HandleOrderCancelledAsync(msg, CancellationToken.None);

        var product = await _db.Products.FindAsync(1);
        Assert.Equal(55, product!.Stock);
    }

    [Fact]
    public async Task HandleOrderCancelledAsync_MultipleItems_RestoresAllStocks()
    {
        var msg = new OrderCancelledMessage(42,
        [
            new OrderItemMessage(1, 10),
            new OrderItemMessage(2, 3)
        ]);

        await _sut.HandleOrderCancelledAsync(msg, CancellationToken.None);

        var product1 = await _db.Products.FindAsync(1);
        var product2 = await _db.Products.FindAsync(2);
        Assert.Equal(60, product1!.Stock);
        Assert.Equal(13, product2!.Stock);
    }

    [Fact]
    public async Task HandleOrderCancelledAsync_UnknownProduct_Throws()
    {
        var msg = new OrderCancelledMessage(42, [new OrderItemMessage(999, 1)]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.HandleOrderCancelledAsync(msg, CancellationToken.None));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
