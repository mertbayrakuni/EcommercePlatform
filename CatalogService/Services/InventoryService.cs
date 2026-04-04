using CatalogService.Data;
using CatalogService.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly CatalogDbContext _db;
    public InventoryService(CatalogDbContext db) => _db = db;

    public async Task<InventoryDecreaseResponseDto> DecreaseStockAsync(InventoryDecreaseRequestDto req, CancellationToken ct = default)
    {
        // Validate request and merge duplicate product lines into aggregate quantities
        if (req.Items is null || req.Items.Count == 0)
            throw new InvalidOperationException("Items cannot be empty.");

        if (req.Items.Any(x => x.Quantity <= 0))
            throw new InvalidOperationException("Quantity must be >= 1.");

        // same product multiple times => merge
        var merged = req.Items
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var ids = merged.Select(x => x.ProductId).ToList();

        var products = await _db.Products
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);

        if (products.Count != ids.Count)
        {
            var foundIds = products.Select(p => p.Id).ToHashSet();
            var missing = ids.Where(id => !foundIds.Contains(id));
            throw new InvalidOperationException($"Missing products: {string.Join(',', missing)}");
        }

        // Subtract the requested quantities from product stocks inside a DB transaction
        foreach (var it in merged)
        {
            var p = products.First(x => x.Id == it.ProductId);

            if (!p.IsActive)
                throw new InvalidOperationException($"Product inactive: {p.Id}");

            if (p.Stock < it.Quantity)
                throw new InvalidOperationException($"Not enough stock for product {p.Id}. Stock={p.Stock}, Need={it.Quantity}");

            p.Stock -= it.Quantity;
            p.UpdatedAt = DateTime.UtcNow; // sende varsa
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new InventoryDecreaseResponseDto
        {
            Items = products.Select(p => new InventoryDecreaseResultDto
            {
                ProductId = p.Id,
                NewStock = p.Stock
            }).ToList()
        };
    }

    public async Task<InventoryDecreaseResponseDto> IncreaseStockAsync(InventoryDecreaseRequestDto req, CancellationToken ct = default)
    {
        // Increase stock for given items (used for cancellations / returns)
        if (req.Items is null || req.Items.Count == 0)
            throw new InvalidOperationException("Items cannot be empty.");

        if (req.Items.Any(x => x.Quantity <= 0))
            throw new InvalidOperationException("Quantity must be >= 1.");

        var merged = req.Items
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var ids = merged.Select(x => x.ProductId).ToList();

        var products = await _db.Products
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);

        if (products.Count != ids.Count)
        {
            var foundIds = products.Select(p => p.Id).ToHashSet();
            var missing = ids.Where(id => !foundIds.Contains(id));
            throw new InvalidOperationException($"Missing products: {string.Join(',', missing)}");
        }

        foreach (var it in merged)
        {
            var p = products.First(x => x.Id == it.ProductId);
            if (!p.IsActive)
                throw new InvalidOperationException($"Product inactive: {p.Id}");

            p.Stock += it.Quantity;
            p.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new InventoryDecreaseResponseDto
        {
            Items = products.Select(p => new InventoryDecreaseResultDto
            {
                ProductId = p.Id,
                NewStock = p.Stock
            }).ToList()
        };
    }
}