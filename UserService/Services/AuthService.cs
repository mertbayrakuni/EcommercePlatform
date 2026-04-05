using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserService.Data;
using UserService.Dtos;
using UserService.Models;

namespace UserService.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(UserDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email == email, ct);
        if (exists)
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Role = await _db.Users.AnyAsync(ct) ? UserRoles.Customer : UserRoles.Admin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return BuildResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new InvalidOperationException("Invalid email or password.");

        return BuildResponse(user);
    }

    public async Task<UserProfileDto?> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }

    private AuthResponseDto BuildResponse(User user)
    {
        var expiryHours = double.TryParse(_config["Jwt:ExpiryHours"], out var h) ? h : 24;
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

        return new AuthResponseDto
        {
            Token = GenerateJwt(user, expiresAt),
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            ExpiresAt = expiresAt
        };
    }

    public async Task<UserProfileDto> ChangeRoleAsync(int userId, string newRole, CancellationToken ct = default)
    {
        if (newRole != UserRoles.Admin && newRole != UserRoles.Customer)
            throw new InvalidOperationException($"Invalid role '{newRole}'. Valid roles: {UserRoles.Admin}, {UserRoles.Customer}.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        user.Role = newRole;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }

    private string GenerateJwt(User user, DateTime expiresAt)
    {
        var secret = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
