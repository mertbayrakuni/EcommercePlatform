using CatalogService.Data;
using CatalogService.Dtos;
using CatalogService.Models;
using CatalogService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Tests;

public class InventoryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CatalogDbContext _db;
    private readonly InventoryService _sut;

    public InventoryServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var opts = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new CatalogDbContext(opts);
        _db.Database.EnsureCreated();

        SeedData();

        _sut = new InventoryService(_db);
    }

    // ── seed helpers ──────────────────────────────────────────────────────────

    private void SeedData()
    {
        var category = new Category { Name = "Electronics", Slug = "electronics" };
        _db.Categories.Add(category);
        _db.SaveChanges();

        _db.Products.AddRange(
            new Product { Id = 1, Name = "Widget A", Sku = "SKU-001", Price = 9.99m, Stock = 50, IsActive = true, CategoryId = category.Id },
            new Product { Id = 2, Name = "Widget B", Sku = "SKU-002", Price = 19.99m, Stock = 10, IsActive = true, CategoryId = category.Id },
            new Product { Id = 3, Name = "Widget C", Sku = "SKU-003", Price = 4.99m, Stock = 5, IsActive = false, CategoryId = category.Id }
        );
        _db.SaveChanges();
    }

    private static InventoryDecreaseRequestDto Req(params (int id, int qty)[] items) =>
        new InventoryDecreaseRequestDto
        {
            Items = items.Select(x => new InventoryDecreaseItemDto { ProductId = x.id, Quantity = x.qty }).ToList()
        };

    // ── DecreaseStockAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DecreaseStockAsync_HappyPath_ReducesStockAndReturnsResult()
    {
        var result = await _sut.DecreaseStockAsync(Req((1, 5)));

        Assert.Single(result.Items);
        Assert.Equal(45, result.Items[0].NewStock);
        Assert.Equal(1, result.Items[0].ProductId);

        var product = await _db.Products.FindAsync(1);
        Assert.Equal(45, product!.Stock);
    }

    [Fact]
    public async Task DecreaseStockAsync_MultipleProducts_ReducesAllStocks()
    {
        var result = await _sut.DecreaseStockAsync(Req((1, 10), (2, 3)));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(40, result.Items.First(x => x.ProductId == 1).NewStock);
        Assert.Equal(7, result.Items.First(x => x.ProductId == 2).NewStock);
    }

    [Fact]
    public async Task DecreaseStockAsync_DuplicateProductLines_MergesQuantities()
    {
        var result = await _sut.DecreaseStockAsync(Req((1, 5), (1, 3)));

        Assert.Single(result.Items);
        Assert.Equal(42, result.Items[0].NewStock);
    }

    [Fact]
    public async Task DecreaseStockAsync_NotEnoughStock_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DecreaseStockAsync(Req((2, 20))));

        Assert.Contains("Not enough stock", ex.Message);
    }

    [Fact]
    public async Task DecreaseStockAsync_ProductNotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DecreaseStockAsync(Req((999, 1))));

        Assert.Contains("Missing products", ex.Message);
    }

    [Fact]
    public async Task DecreaseStockAsync_InactiveProduct_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DecreaseStockAsync(Req((3, 1))));

        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecreaseStockAsync_EmptyItems_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DecreaseStockAsync(new InventoryDecreaseRequestDto { Items = new() }));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecreaseStockAsync_ZeroQuantity_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DecreaseStockAsync(Req((1, 0))));

        Assert.Contains("Quantity", ex.Message);
    }

    // ── IncreaseStockAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task IncreaseStockAsync_HappyPath_AddsToStock()
    {
        var result = await _sut.IncreaseStockAsync(Req((1, 10)));

        Assert.Equal(60, result.Items[0].NewStock);

        var product = await _db.Products.FindAsync(1);
        Assert.Equal(60, product!.Stock);
    }

    [Fact]
    public async Task IncreaseStockAsync_MultipleProducts_AddsToAllStocks()
    {
        var result = await _sut.IncreaseStockAsync(Req((1, 5), (2, 5)));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(55, result.Items.First(x => x.ProductId == 1).NewStock);
        Assert.Equal(15, result.Items.First(x => x.ProductId == 2).NewStock);
    }

    [Fact]
    public async Task IncreaseStockAsync_ProductNotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.IncreaseStockAsync(Req((999, 5))));

        Assert.Contains("Missing products", ex.Message);
    }

    [Fact]
    public async Task IncreaseStockAsync_InactiveProduct_StillRestoresStock()
    {
        // Product 3 is inactive — stock restore on cancellation must succeed regardless
        var result = await _sut.IncreaseStockAsync(Req((3, 2)));

        Assert.Equal(7, result.Items[0].NewStock);

        var product = await _db.Products.FindAsync(3);
        Assert.Equal(7, product!.Stock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
