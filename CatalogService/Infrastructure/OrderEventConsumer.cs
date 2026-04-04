using CatalogService.Dtos;
using CatalogService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace CatalogService.Infrastructure;

public sealed class OrderEventConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<OrderEventConsumer> _logger;

    private IConnection? _conn;
    private IChannel? _channel;

    public OrderEventConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<OrderEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                attempt++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                _logger.LogError(ex, "RabbitMQ consumer failed (attempt {Attempt}). Reconnecting in {Delay}s…", attempt, delay.TotalSeconds);

                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "rabbitmq",
            UserName = _config["RabbitMQ:User"] ?? "guest",
            Password = _config["RabbitMQ:Pass"] ?? "guest"
        };

        _conn = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _conn.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declare the exchange — must match what OrderService declares
        await _channel.ExchangeDeclareAsync(
            exchange: "orders",
            type: ExchangeType.Direct,
            durable: true,
            cancellationToken: stoppingToken);

        // Durable queue survives broker restarts
        await _channel.QueueDeclareAsync(
            queue: "catalog.inventory",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: "catalog.inventory",
            exchange: "orders",
            routingKey: "order.cancelled",
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<OrderCancelledMessage>(json);

                if (evt is not null)
                    await HandleOrderCancelledAsync(evt, stoppingToken);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process order.cancelled (delivery tag {Tag})", ea.DeliveryTag);
                // Discard poison messages — no infinite retry loop
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: "catalog.inventory",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("OrderEventConsumer started — listening on queue 'catalog.inventory'");

        try
        {
            // Hold the hosted service open until the app shuts down or connection drops
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        finally
        {
            if (_channel is not null) try { await _channel.DisposeAsync(); } catch { }
            if (_conn is not null) try { await _conn.DisposeAsync(); } catch { }
        }
    }

    internal async Task HandleOrderCancelledAsync(OrderCancelledMessage evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        var req = new InventoryDecreaseRequestDto
        {
            Items = evt.Items.Select(i => new InventoryDecreaseItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        await inventory.IncreaseStockAsync(req, ct);

        _logger.LogInformation("Stock restored for cancelled order {OrderId}", evt.OrderId);
    }
}

// Internal DTOs — mirror the fields OrderService serialises into the event payload
internal sealed record OrderCancelledMessage(int OrderId, List<OrderItemMessage> Items);
internal sealed record OrderItemMessage(int ProductId, int Quantity);
