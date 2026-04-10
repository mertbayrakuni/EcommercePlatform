using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OrderService.Data;
using OrderService.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using OrderService.Services;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

// Serilog
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
        doc.Info.Title = "OrderService API";
        doc.Info.Description = "Order placement, tracking & cancellation";
        doc.Info.Contact = new OpenApiContact
        {
            Name = "ECommercePlatform",
            Url = new Uri("https://github.com/mertbayrakuni/EcommercePlatform")
        };
        doc.Servers =
        [
            new OpenApiServer { Url = "http://localhost:5099", Description = "Local development" },
            new OpenApiServer { Url = "http://orderservice:8080", Description = "Docker" }
        ];
        doc.Tags = new HashSet<OpenApiTag>
        {
            new OpenApiTag { Name = "Orders", Description = "Order placement, status tracking & cancellation" }
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, ct) =>
    {
        var path = context.Description.RelativePath ?? "";
        var method = context.Description.HttpMethod ?? "";
        if (path.Contains("orders", StringComparison.OrdinalIgnoreCase)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var createOrderBody) == true)
        {
            createOrderBody.Example = JsonNode.Parse("""{"customerEmail":"customer@example.com","items":[{"productId":1,"quantity":2},{"productId":3,"quantity":1}]}""");
        }
        return Task.CompletedTask;
    });
});

// Register the named HttpClient used to call CatalogService. Polly policies
// are configured to improve resilience on transient network failures.

// DbContext
builder.Services.AddDbContext<OrderDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("OrderDb")));

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>();

// HttpClient for CatalogService (docker internal URL later) with Polly policies
builder.Services.AddHttpClient("Catalog", client =>
{
    var baseUrl = builder.Configuration["CatalogService:BaseUrl"];
    client.BaseAddress = new Uri(baseUrl!);
})
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3) }))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// also register Payment named client
builder.Services.AddHttpClient("Payment", client =>
{
    var baseUrl = builder.Configuration["PaymentService:BaseUrl"] ?? "http://paymentservice:8080";
    client.BaseAddress = new Uri(baseUrl);
})
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3) }))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

builder.Services.AddScoped<IOrderService, OrderService.Services.OrderService>();

// RabbitMQ: register a lightweight publisher helper
builder.Services.AddSingleton<IRabbitPublisher, RabbitPublisher>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<RabbitPublisher>>();
    var factory = new RabbitMQ.Client.ConnectionFactory
    {
        HostName = cfg["RabbitMQ:Host"] ?? "rabbitmq",
        UserName = cfg["RabbitMQ:User"] ?? "guest",
        Password = cfg["RabbitMQ:Pass"] ?? "guest"
    };
    return new RabbitPublisher(factory, logger);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.MigrateAsync();
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("OrderService API")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultFonts(false)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .WithPreferredScheme("Bearer")
        .WithHttpBearerAuthentication(bearer => bearer.Token = "paste-your-jwt-token-here");
});

app.MapHealthChecks("/health");

app.MapControllers();
app.MapGet("/", () => "OrderService is running ✅");

app.Run();