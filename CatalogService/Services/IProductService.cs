using CatalogService.Common;
using CatalogService.Dtos;

namespace CatalogService.Services;

/// <summary>
/// CRUD operations for catalogue products including filtering, sorting and pagination.
/// </summary>
public interface IProductService
{
    /// <summary>Returns a paginated, optionally filtered and sorted list of products.</summary>
    Task<PagedResult<ProductResponseDto>> GetAllAsync(
        int page,
        int pageSize,
        string? search,
        int? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        bool includeInactive,
        string sort
    );

    /// <summary>Returns a single product by ID, or <c>null</c> if not found.</summary>
    Task<ProductResponseDto?> GetByIdAsync(int id, bool includeInactive = false);

    /// <summary>Creates a new product and returns the created entity wrapped in a <see cref="ServiceResult{T}"/>.</summary>
    Task<ServiceResult<ProductResponseDto>> CreateAsync(ProductCreateDto dto);

    /// <summary>Updates an existing product. Returns <c>NotFound</c> if the product does not exist.</summary>
    Task<ServiceResult<ProductResponseDto>> UpdateAsync(int id, ProductUpdateDto dto);

    /// <summary>Soft-deletes a product by setting <c>IsActive = false</c>.</summary>
    Task<ServiceResult<bool>> SoftDeleteAsync(int id);
}