using CatalogService.Common;
using CatalogService.Dtos;

namespace CatalogService.Services;

public interface ICategoryService
{
    Task<List<CategoryResponseDto>> GetAllAsync(bool includeInactive = false);

    Task<CategoryResponseDto?> GetByIdAsync(int id, bool includeInactive = false);

    Task<ServiceResult<CategoryResponseDto>> CreateAsync(CategoryCreateDto dto);

    Task<ServiceResult<CategoryResponseDto>> UpdateAsync(int id, CategoryUpdateDto dto);

    Task<ServiceResult<bool>> SoftDeleteAsync(int id);
}