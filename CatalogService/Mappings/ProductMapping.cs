using CatalogService.Dtos;
using CatalogService.Models;

namespace CatalogService.Mappings;

public static class ProductMapping
{
    public static ProductResponseDto ToDto(this Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        Stock = p.Stock,
        Description = p.Description,
        Sku = p.Sku,
        ImageUrl = p.ImageUrl,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name,
        CategorySlug = p.Category?.Slug
    };

    // Optional: for Create
    public static Product ToEntity(this ProductCreateDto dto) => new()
    {
        Name = dto.Name,
        Price = dto.Price,
        Stock = dto.Stock,
        Description = dto.Description,
        Sku = dto.Sku,
        ImageUrl = dto.ImageUrl,
        CategoryId = dto.CategoryId,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // For Update
    public static void ApplyUpdate(this Product entity, ProductUpdateDto dto)
    {
        entity.Name = dto.Name;
        entity.Price = dto.Price;
        entity.Stock = dto.Stock;
        entity.Description = dto.Description;
        entity.Sku = dto.Sku;
        entity.ImageUrl = dto.ImageUrl;
        entity.CategoryId = dto.CategoryId;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }
}