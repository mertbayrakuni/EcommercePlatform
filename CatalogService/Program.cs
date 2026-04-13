using CatalogService.Data;
using CatalogService.Infrastructure;
using CatalogService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Controllers + JSON (enums as strings in API responses)
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Info.Title = "CatalogService API";
        doc.Info.Description = "Products, categories & inventory management";
        doc.Info.Contact = new OpenApiContact
        {
            Name = "ECommercePlatform",
            Url = new Uri("https://github.com/mertbayrakuni/EcommercePlatform")
        };
        doc.Servers =
        [
            new OpenApiServer { Url = "http://localhost:5098", Description = "Local development" },
            new OpenApiServer { Url = "http://catalogservice:8080", Description = "Docker" }
        ];
        doc.Tags = new HashSet<OpenApiTag>
        {
            new OpenApiTag { Name = "Products", Description = "Product catalogue, search & CRUD" },
            new OpenApiTag { Name = "Categories", Description = "Category hierarchy & slug management" },
            new OpenApiTag { Name = "Inventory", Description = "Stock levels & reservation" }
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, ct) =>
    {
        var path = context.Description.RelativePath ?? "";
        var method = context.Description.HttpMethod ?? "";
        if (path.Contains("products", StringComparison.OrdinalIgnoreCase)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var createProductBody) == true)
        {
            createProductBody!.Example = JsonNode.Parse("""{"name":"Running Shoes","price":89.99,"stock":50,"description":"Lightweight trail running shoes","sku":"SHOE-001","imageUrl":"https://example.com/shoes.jpg","categoryId":1}""");
        }
        if (path.Contains("products", StringComparison.OrdinalIgnoreCase)
            && method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var updateProductBody) == true)
        {
            updateProductBody!.Example = JsonNode.Parse("""{"name":"Running Shoes Pro","price":99.99,"stock":45,"description":"Updated lightweight trail running shoes","sku":"SHOE-001","imageUrl":"https://example.com/shoes.jpg","isActive":true,"categoryId":1}""");
        }
        if (path.Contains("categories", StringComparison.OrdinalIgnoreCase)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var createCatBody) == true)
        {
            createCatBody!.Example = JsonNode.Parse("""{"name":"Footwear","slug":"footwear"}""");
        }
        if (path.Contains("categories", StringComparison.OrdinalIgnoreCase)
            && method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var updateCatBody) == true)
        {
            updateCatBody!.Example = JsonNode.Parse("""{"name":"Footwear & Shoes","slug":"footwear-shoes","isActive":true}""");
        }
        if (path.Contains("inventory/decrease", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var decreaseBody) == true)
        {
            decreaseBody!.Example = JsonNode.Parse("""{"items":[{"productId":1,"quantity":2}]}""");
        }
        if (path.Contains("inventory/increase", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var increaseBody) == true)
        {
            increaseBody!.Example = JsonNode.Parse("""{"items":[{"productId":1,"quantity":2}]}""");
        }
        return Task.CompletedTask;
    });
});

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CatalogDb")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();

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

builder.Services.AddHostedService<OrderEventConsumer>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("CatalogService API")
        .WithTheme(ScalarTheme.DeepSpace)
        .DisableDefaultFonts()
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .AddPreferredSecuritySchemes("Bearer")
        .AddHttpAuthentication("Bearer", scheme => scheme.Token = app.Configuration["Scalar:BearerToken"] ?? "");
});

// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet("/", () => "CatalogService is running ✅");

app.Run();