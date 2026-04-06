using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UserService.Data;
using UserService.Dtos;
using UserService.Services;

namespace UserService.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly UserDbContext _db;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var opts = new DbContextOptionsBuilder<UserDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new UserDbContext(opts);
        _db.Database.EnsureCreated();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-at-least-32-characters-long-for-hmac",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpiryHours"] = "1"
            })
            .Build();

        _sut = new AuthService(_db, config);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static RegisterRequestDto Reg(
        string email = "test@example.com",
        string password = "Password123",
        string firstName = "Test",
        string lastName = "User") =>
        new() { Email = email, Password = password, FirstName = firstName, LastName = lastName };

    // ── RegisterAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_HappyPath_ReturnsTokenAndCreatesUser()
    {
        var result = await _sut.RegisterAsync(Reg());

        Assert.NotEmpty(result.Token);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal(1, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_Throws()
    {
        await _sut.RegisterAsync(Reg());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(Reg()));

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterAsync_EmailIsNormalizedToLowercase()
    {
        var result = await _sut.RegisterAsync(Reg(email: "Test@Example.COM"));

        Assert.Equal("test@example.com", result.Email);
        var user = await _db.Users.SingleAsync();
        Assert.Equal("test@example.com", user.Email);
    }

    [Fact]
    public async Task RegisterAsync_FirstUser_HasAdminRole()
    {
        // Empty DB — the very first registration should receive Admin
        var result = await _sut.RegisterAsync(Reg());

        Assert.Equal("Admin", result.Role);
    }

    [Fact]
    public async Task RegisterAsync_NewUser_HasCustomerRole()
    {
        // Pre-seed a first user so this registration is not the first
        await _sut.RegisterAsync(Reg(email: "first@example.com"));

        var result = await _sut.RegisterAsync(Reg());

        Assert.Equal("Customer", result.Role);
    }

    [Fact]
    public async Task RegisterAsync_PasswordIsHashed_NotStoredInPlainText()
    {
        await _sut.RegisterAsync(Reg(password: "MySecretPassword"));

        var user = await _db.Users.SingleAsync();
        Assert.NotEqual("MySecretPassword", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("MySecretPassword", user.PasswordHash));
    }

    // ── LoginAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        await _sut.RegisterAsync(Reg());

        var result = await _sut.LoginAsync(new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "Password123"
        });

        Assert.NotEmpty(result.Token);
        Assert.Equal("test@example.com", result.Email);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Throws()
    {
        await _sut.RegisterAsync(Reg());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LoginAsync(new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            }));

        Assert.Contains("Invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.LoginAsync(new LoginRequestDto
            {
                Email = "nobody@example.com",
                Password = "Password123"
            }));

        Assert.Contains("Invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_EmailCaseInsensitive_Succeeds()
    {
        await _sut.RegisterAsync(Reg(email: "test@example.com"));

        var result = await _sut.LoginAsync(new LoginRequestDto
        {
            Email = "TEST@EXAMPLE.COM",
            Password = "Password123"
        });

        Assert.NotEmpty(result.Token);
    }

    // ── GetProfileAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfileAsync_ValidUser_ReturnsProfile()
    {
        await _sut.RegisterAsync(Reg(firstName: "İpek", lastName: "B"));

        var user = await _db.Users.SingleAsync();
        var profile = await _sut.GetProfileAsync(user.Id);

        Assert.NotNull(profile);
        Assert.Equal("İpek", profile.FirstName);
        Assert.Equal("test@example.com", profile.Email);
    }

    [Fact]
    public async Task GetProfileAsync_UnknownUser_ReturnsNull()
    {
        var profile = await _sut.GetProfileAsync(999);

        Assert.Null(profile);
    }

    // ── ChangeRoleAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeRoleAsync_ValidRole_UpdatesRole()
    {
        await _sut.RegisterAsync(Reg()); // first user → Admin
        var user = await _db.Users.SingleAsync();

        var result = await _sut.ChangeRoleAsync(user.Id, "Customer");

        Assert.Equal("Customer", result.Role);
        Assert.Equal("Customer", (await _db.Users.SingleAsync()).Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_InvalidRole_Throws()
    {
        await _sut.RegisterAsync(Reg());
        var user = await _db.Users.SingleAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ChangeRoleAsync(user.Id, "SuperAdmin"));

        Assert.Contains("Invalid role", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeRoleAsync_UnknownUser_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.ChangeRoleAsync(999, "Customer"));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
