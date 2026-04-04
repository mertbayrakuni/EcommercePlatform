using CatalogService.Dtos;
using CatalogService.Models;

namespace CatalogService.Mappings;

public static class CategoryMapping
{
    public static CategoryResponseDto ToDto(this Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Slug = c.Slug,
        IsActive = c.IsActive
    };

    // Optional: for Create
    public static Category ToEntity(this CategoryCreateDto dto) => new()
    {
        Name = dto.Name,
        Slug = dto.Slug,
        IsActive = true
    };

    // For Update
    public static void ApplyUpdate(this Category entity, CategoryUpdateDto dto)
    {
        entity.Name = dto.Name;
        entity.Slug = dto.Slug;
        entity.IsActive = dto.IsActive;
    }
}