using CatalogService.Data;
using CatalogService.Infrastructure;
using CatalogService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
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
});

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CatalogDb")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();

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
        .WithDefaultFonts(false)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .WithPreferredScheme("Bearer")
        .WithHttpBearerAuthentication(bearer => bearer.Token = "paste-your-jwt-token-here");
});

// app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet("/", () => "CatalogService is running ✅");

app.Run();