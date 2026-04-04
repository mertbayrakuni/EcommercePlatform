using System.ComponentModel.DataAnnotations;

namespace CatalogService.Dtos;

public class CategoryUpdateDto
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}