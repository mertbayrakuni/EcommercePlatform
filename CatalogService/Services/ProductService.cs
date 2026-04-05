using CatalogService.Common;
using CatalogService.Data;
using CatalogService.Dtos;
using CatalogService.Mappings;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Services;

/// <summary>
/// Handles product queries and mutations. Supports rich filtering (search, category,
/// price range), multiple sort orders and pagination. Inactive products are hidden
/// from public results unless <c>includeInactive</c> is explicitly requested.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly CatalogDbContext _db;
    public ProductService(CatalogDbContext db) => _db = db;

    public async Task<PagedResult<ProductResponseDto>> GetAllAsync(
        int page,
        int pageSize,
        string? search,
        int? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        bool includeInactive,
        string sort
    )
    {
        page = Paging.ClampPage(page);
        pageSize = Paging.ClampPageSize(pageSize, defaultSize: 10, maxSize: 200);

        var q = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        if (!includeInactive)
            q = q.Where(p => p.IsActive);

        if (categoryId.HasValue)
            q = q.Where(p => p.CategoryId == categoryId.Value);

        if (minPrice.HasValue)
            q = q.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            q = q.Where(p => p.Price <= maxPrice.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p =>
                EF.Functions.ILike(p.Name, $"%{s}%") ||
                (p.Description != null && EF.Functions.ILike(p.Description, $"%{s}%")) ||
                EF.Functions.ILike(p.Sku, $"%{s}%")
            );
        }

        q = sort?.ToLowerInvariant() switch
        {
            "newest" => q.OrderByDescending(p => p.CreatedAt),
            "oldest" => q.OrderBy(p => p.CreatedAt),

            "price_asc" => q.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price_desc" => q.OrderByDescending(p => p.Price).ThenBy(p => p.Id),

            "name_asc" => q.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "name_desc" => q.OrderByDescending(p => p.Name).ThenBy(p => p.Id),

            "stock_asc" => q.OrderBy(p => p.Stock).ThenBy(p => p.Id),
            "stock_desc" => q.OrderByDescending(p => p.Stock).ThenBy(p => p.Id),

            _ => q.OrderByDescending(p => p.CreatedAt)
        };

        var totalCount = await q.CountAsync();

        var entities = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ProductResponseDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = entities.Select(p => p.ToDto()).ToList()
        };
    }

    public async Task<ProductResponseDto?> GetByIdAsync(int id, bool includeInactive = false)
    {
        var q = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Id == id);

        if (!includeInactive)
            q = q.Where(p => p.IsActive);

        var entity = await q.FirstOrDefaultAsync();
        return entity?.ToDto();
    }

    public async Task<ServiceResult<ProductResponseDto>> CreateAsync(ProductCreateDto dto)
    {
        // normalize (optional but recommended)
        dto.Name = dto.Name.Trim();
        dto.Sku = dto.Sku.Trim().ToUpperInvariant();

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == dto.CategoryId && c.IsActive);

        if (!categoryExists)
            return ServiceResult<ProductResponseDto>.Validation("CategoryId is invalid.");

        var skuExists = await _db.Products
            .AnyAsync(p => p.Sku == dto.Sku);

        if (skuExists)
            return ServiceResult<ProductResponseDto>.Conflict("SKU already exists.");

        var entity = dto.ToEntity(); // ✅ mapping used

        _db.Products.Add(entity);
        await _db.SaveChangesAsync();

        // reload category for response (CategoryName/Slug)
        await _db.Entry(entity).Reference(x => x.Category).LoadAsync();

        return ServiceResult<ProductResponseDto>.SuccessResult(entity.ToDto());
    }

    public async Task<ServiceResult<ProductResponseDto>> UpdateAsync(int id, ProductUpdateDto dto)
    {
        dto.Name = dto.Name.Trim();
        dto.Sku = dto.Sku.Trim().ToUpperInvariant();

        var entity = await _db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (entity is null)
            return ServiceResult<ProductResponseDto>.NotFound("Product not found.");

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.Id == dto.CategoryId && c.IsActive);

        if (!categoryExists)
            return ServiceResult<ProductResponseDto>.Validation("CategoryId is invalid.");

        var skuExists = await _db.Products
            .AnyAsync(p => p.Id != id && p.Sku == dto.Sku);

        if (skuExists)
            return ServiceResult<ProductResponseDto>.Conflict("SKU already exists.");

        entity.ApplyUpdate(dto); // ✅ mapping used

        await _db.SaveChangesAsync();

        // ensure category loaded after CategoryId change
        await _db.Entry(entity).Reference(x => x.Category).LoadAsync();

        return ServiceResult<ProductResponseDto>.SuccessResult(entity.ToDto());
    }

    public async Task<ServiceResult<bool>> SoftDeleteAsync(int id)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null)
            return ServiceResult<bool>.NotFound("Product not found.");

        if (!entity.IsActive)
            return ServiceResult<bool>.SuccessResult(true);

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return ServiceResult<bool>.SuccessResult(true);
    }
}