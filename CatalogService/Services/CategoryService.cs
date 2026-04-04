using CatalogService.Common;
using CatalogService.Data;
using CatalogService.Dtos;
using CatalogService.Mappings;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly CatalogDbContext _db;

    public CategoryService(CatalogDbContext db)
        => _db = db;

    public async Task<List<CategoryResponseDto>> GetAllAsync(bool includeInactive = false)
    {
        var q = _db.Categories.AsNoTracking().AsQueryable();

        if (!includeInactive)
            q = q.Where(c => c.IsActive);

        var items = await q
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        return items.Select(c => c.ToDto()).ToList();
    }

    public async Task<CategoryResponseDto?> GetByIdAsync(int id, bool includeInactive = false)
    {
        var q = _db.Categories.AsNoTracking().Where(c => c.Id == id);

        if (!includeInactive)
            q = q.Where(c => c.IsActive);

        var entity = await q.FirstOrDefaultAsync();
        return entity?.ToDto();
    }

    public async Task<ServiceResult<CategoryResponseDto>> CreateAsync(CategoryCreateDto dto)
    {
        dto.Name = dto.Name.Trim();
        dto.Slug = dto.Slug.Trim().ToLowerInvariant();

        var slugExists = await _db.Categories
            .AnyAsync(c => c.Slug == dto.Slug);

        if (slugExists)
            return ServiceResult<CategoryResponseDto>.Conflict("Slug already exists.");

        var entity = dto.ToEntity();

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync();

        return ServiceResult<CategoryResponseDto>.SuccessResult(entity.ToDto());
    }

    public async Task<ServiceResult<CategoryResponseDto>> UpdateAsync(int id, CategoryUpdateDto dto)
    {
        dto.Name = dto.Name.Trim();
        dto.Slug = dto.Slug.Trim().ToLowerInvariant();

        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (entity is null)
            return ServiceResult<CategoryResponseDto>.NotFound("Category not found.");

        var slugExists = await _db.Categories.AnyAsync(c =>
            c.Id != id && c.Slug == dto.Slug);

        if (slugExists)
            return ServiceResult<CategoryResponseDto>.Conflict("Slug already exists.");

        entity.ApplyUpdate(dto);

        await _db.SaveChangesAsync();

        return ServiceResult<CategoryResponseDto>.SuccessResult(entity.ToDto());
    }

    public async Task<ServiceResult<bool>> SoftDeleteAsync(int id)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (entity is null)
            return ServiceResult<bool>.NotFound("Category not found.");

        if (!entity.IsActive)
            return ServiceResult<bool>.SuccessResult(true);

        entity.IsActive = false;
        await _db.SaveChangesAsync();

        return ServiceResult<bool>.SuccessResult(true);
    }
}