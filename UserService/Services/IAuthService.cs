using UserService.Dtos;

namespace UserService.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
    Task<UserProfileDto?> GetProfileAsync(int userId, CancellationToken ct = default);
}
