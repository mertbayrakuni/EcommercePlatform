using CatalogService.Common;
using CatalogService.Dtos;

namespace CatalogService.Services;

public interface IProductService
{
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

    Task<ProductResponseDto?> GetByIdAsync(int id, bool includeInactive = false);

    Task<ServiceResult<ProductResponseDto>> CreateAsync(ProductCreateDto dto);

    Task<ServiceResult<ProductResponseDto>> UpdateAsync(int id, ProductUpdateDto dto);

    Task<ServiceResult<bool>> SoftDeleteAsync(int id);
}