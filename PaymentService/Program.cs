using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using PaymentService.Data;
using PaymentService.Services;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json.Nodes;

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
        doc.Info.Title = "PaymentService API";
        doc.Info.Description = "Payment processing & transaction records";
        doc.Info.Contact = new OpenApiContact
        {
            Name = "ECommercePlatform",
            Url = new Uri("https://github.com/mertbayrakuni/EcommercePlatform")
        };
        doc.Servers =
        [
            new OpenApiServer { Url = "http://localhost:5100", Description = "Local development" },
            new OpenApiServer { Url = "http://paymentservice:8080", Description = "Docker" }
        ];
        doc.Tags = new HashSet<OpenApiTag>
        {
            new OpenApiTag { Name = "Payments", Description = "Payment processing & transaction history" }
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, ct) =>
    {
        var path = context.Description.RelativePath ?? "";
        if (path.Contains("payments/pay", StringComparison.OrdinalIgnoreCase)
            && operation.RequestBody?.Content.TryGetValue("application/json", out var payBody) == true)
        {
            payBody.Example = JsonNode.Parse("""{"orderId":1,"amount":179.97,"method":"CreditCard","simulateFailure":false}""");
        }
        return Task.CompletedTask;
    });
});

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb")));

builder.Services.AddScoped<IPaymentProcessor, PaymentProcessor>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.MigrateAsync();
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("PaymentService API")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultFonts(false)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .WithPreferredScheme("Bearer")
        .WithHttpBearerAuthentication(bearer => bearer.Token = "paste-your-jwt-token-here");
});

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => "PaymentService is running ✅");
app.Run();
