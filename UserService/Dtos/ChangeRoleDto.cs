using System.ComponentModel.DataAnnotations;

namespace UserService.Dtos;

public sealed class ChangeRoleDto
{
    [Required]
    public string Role { get; set; } = string.Empty;
}
