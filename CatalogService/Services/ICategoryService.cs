using CatalogService.Common;
using CatalogService.Dtos;

namespace CatalogService.Services;

/// <summary>
/// CRUD operations for product categories.
/// </summary>
public interface ICategoryService
{
    /// <summary>Returns all categories, optionally including inactive ones.</summary>
    Task<List<CategoryResponseDto>> GetAllAsync(bool includeInactive = false);

    /// <summary>Returns a single category by ID, or <c>null</c> if not found.</summary>
    Task<CategoryResponseDto?> GetByIdAsync(int id, bool includeInactive = false);

    /// <summary>Creates a new category. Returns <c>Conflict</c> if the slug is already taken.</summary>
    Task<ServiceResult<CategoryResponseDto>> CreateAsync(CategoryCreateDto dto);

    /// <summary>Updates an existing category. Returns <c>NotFound</c> if it does not exist.</summary>
    Task<ServiceResult<CategoryResponseDto>> UpdateAsync(int id, CategoryUpdateDto dto);

    /// <summary>Soft-deletes a category by setting <c>IsActive = false</c>.</summary>
    Task<ServiceResult<bool>> SoftDeleteAsync(int id);
}