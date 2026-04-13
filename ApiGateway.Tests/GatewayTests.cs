using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Tests;

public class GatewayTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private const string JwtSecret = "super-secret-development-key-change-in-production-32chars!";
    private const string JwtIssuer = "ECommercePlatform";
    private const string JwtAudience = "ECommercePlatform";

    public GatewayTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ── /health ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_BodyContainsStatusAndServiceName()
    {
        var body = await (await CreateClient().GetAsync("/health")).Content.ReadAsStringAsync();

        Assert.Contains("healthy", body);
        Assert.Contains("ApiGateway", body);
    }

    // ── / dashboard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_ReturnsOkWithHtmlContentType()
    {
        var response = await CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Dashboard_BodyContainsAllServiceNames()
    {
        var body = await (await CreateClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("UserService", body);
        Assert.Contains("CatalogService", body);
        Assert.Contains("OrderService", body);
        Assert.Contains("PaymentService", body);
    }

    // ── JWT enforcement ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/orders/1")]
    [InlineData("/api/payments/pay")]
    [InlineData("/api/inventory/decrease")]
    public async Task ProtectedRoute_NoToken_Returns401(string path)
    {
        var response = await CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/orders/1")]
    [InlineData("/api/payments/pay")]
    [InlineData("/api/inventory/decrease")]
    public async Task ProtectedRoute_ValidToken_PassesAuth(string path)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BuildJwt());

        var response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_InvalidToken_Returns401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt");

        var response = await client.GetAsync("/api/orders/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_ExpiredToken_Returns401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BuildJwt(expiredMinutesAgo: 10));

        var response = await client.GetAsync("/api/orders/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── public routes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AuthRoute_NoToken_DoesNotReturn401()
    {
        var response = await CreateClient().PostAsync(
            "/api/auth/login",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/categories")]
    public async Task PublicCatalogRoute_NoToken_DoesNotReturn401(string path)
    {
        var response = await CreateClient().GetAsync(path);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string BuildJwt(int expiredMinutesAgo = 0)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = expiredMinutesAgo > 0
            ? DateTime.UtcNow.AddMinutes(-expiredMinutesAgo)
            : DateTime.UtcNow.AddHours(1);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: [new Claim(ClaimTypes.Name, "test@example.com"), new Claim(ClaimTypes.Role, "Customer")],
            expires: expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
