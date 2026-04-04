using System.ComponentModel.DataAnnotations;

namespace CatalogService.Dtos
{
    public class CategoryCreateDto
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;
        [Required, MaxLength(150)]
        public string Slug { get; set; } = string.Empty;
    }
}
