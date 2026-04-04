using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Infrastructure;
using Serilog;
using OrderService.Services;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

app.MapControllers();
app.MapGet("/", () => "OrderService is running ✅");

app.Run();