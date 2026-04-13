using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using UserService.Data;
using UserService.Services;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Info.Title = "UserService API";
        doc.Info.Description = "JWT authentication, registration & user management";
        doc.Info.Contact = new OpenApiContact
        {
            Name = "ECommercePlatform",
            Url = new Uri("https://github.com/mertbayrakuni/EcommercePlatform")
        };
        doc.Servers =
        [
            new OpenApiServer { Url = "http://localhost:5201", Description = "Local development" },
            new OpenApiServer { Url = "http://userservice:8080", Description = "Docker" }
        ];
        doc.Tags = new HashSet<OpenApiTag>
        {
            new OpenApiTag { Name = "Auth", Description = "Register, login & JWT token management" }
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, ct) =>
    {
        var path = context.Description.RelativePath ?? "";
        if (path.Contains("login", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var loginBody) == true)
        {
            loginBody!.Example = JsonNode.Parse("""{"email":"admin@example.com","password":"Password123"}""");
        }
        if (path.Contains("register", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var regBody) == true)
        {
            regBody!.Example = JsonNode.Parse("""{"email":"user@example.com","password":"Password123","firstName":"Demo","lastName":"User"}""");
        }
        return Task.CompletedTask;
    });
});

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("UserDb")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<UserDbContext>();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var secret = builder.Configuration["Jwt:Secret"]!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    await db.Database.MigrateAsync();
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("UserService API")
        .WithTheme(ScalarTheme.DeepSpace)
        .DisableDefaultFonts()
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .AddPreferredSecuritySchemes("Bearer")
        .AddHttpAuthentication("Bearer", scheme => scheme.Token = app.Configuration["Scalar:BearerToken"] ?? "");
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => "UserService is running ✅");

app.Run();
