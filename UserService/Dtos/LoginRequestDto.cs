using System.ComponentModel.DataAnnotations;

namespace UserService.Dtos;

public sealed class LoginRequestDto
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
