using UserService.Dtos;

namespace UserService.Services;

/// <summary>
/// Handles user registration, authentication and profile management.
/// </summary>
public interface IAuthService
{
    /// <summary>Registers a new user. Assigns <c>Admin</c> to the very first user, <c>Customer</c> to all others.</summary>
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default);

    /// <summary>Validates credentials and returns a signed JWT.</summary>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);

    /// <summary>Returns the profile of the given user, or <c>null</c> if not found.</summary>
    Task<UserProfileDto?> GetProfileAsync(int userId, CancellationToken ct = default);

    /// <summary>Changes a user's role. Only <c>Admin</c> and <c>Customer</c> are valid values.</summary>
    Task<UserProfileDto> ChangeRoleAsync(int userId, string newRole, CancellationToken ct = default);
}
