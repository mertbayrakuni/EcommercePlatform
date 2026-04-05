using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace OrderService.Infrastructure;

public interface IRabbitPublisher
{
    Task PublishAsync<T>(string exchange, string routingKey, T payload);
}

public sealed class RabbitPublisher : IRabbitPublisher, IAsyncDisposable
{
    private readonly RabbitMQ.Client.ConnectionFactory _factory;
    private readonly ILogger<RabbitPublisher> _logger;
    private readonly ConcurrentDictionary<string, byte> _declaredExchanges = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private RabbitMQ.Client.IConnection? _conn;
    private RabbitMQ.Client.IChannel? _channel;

    public RabbitPublisher(RabbitMQ.Client.ConnectionFactory factory, ILogger<RabbitPublisher> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_channel is not null) return;
        await _initLock.WaitAsync();
        try
        {
            if (_channel is not null) return;
            _conn = await _factory.CreateConnectionAsync();
            _channel = await _conn.CreateChannelAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task ResetAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_channel is not null) try { await _channel.DisposeAsync(); } catch { }
            if (_conn is not null) try { await _conn.DisposeAsync(); } catch { }
            _channel = null;
            _conn = null;
            _declaredExchanges.Clear();
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task PublishAsync<T>(string exchange, string routingKey, T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await EnsureInitializedAsync();

                if (_declaredExchanges.TryAdd(exchange, 0))
                    await _channel!.ExchangeDeclareAsync(
                        exchange: exchange,
                        type: RabbitMQ.Client.ExchangeType.Direct,
                        durable: true,
                        autoDelete: false);

                await _channel!.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: new RabbitMQ.Client.BasicProperties(),
                    body: body);

                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "Publish attempt {Attempt}/{Max} failed for {Exchange}/{RoutingKey}. Reconnecting…",
                    attempt, maxAttempts, exchange, routingKey);
                await ResetAsync();
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) try { await _channel.DisposeAsync(); } catch { }
        if (_conn is not null) try { await _conn.DisposeAsync(); } catch { }
        _initLock.Dispose();
    }
}
