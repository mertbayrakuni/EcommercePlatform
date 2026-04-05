using System.ComponentModel.DataAnnotations;

namespace CatalogService.Models
{
    /// <summary>
    /// Product category. Uses a URL-friendly <c>Slug</c> alongside the display <c>Name</c>.
    /// </summary>
    public class Category
    {
        public int Id { get; set; }
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;
        [Required, MaxLength(150)]
        public string Slug { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        // Navigation
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
