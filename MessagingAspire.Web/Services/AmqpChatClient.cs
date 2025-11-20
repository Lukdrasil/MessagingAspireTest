// Clean reimplementation targeting RabbitMQ.Client v7 async API.
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessagingAspire.Web.Services;

public class AmqpChatClient : IAsyncDisposable
{
    private readonly IConnection _connection;
    private IChannel? _channel;
    private string? _queueName;
    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? SystemEvent;
    public bool IsConnected { get; private set; }

    public AmqpChatClient(IConnection connection)
    {
        _connection = connection;
    }

    public async Task ConnectAsync(string userDisplayName, string exchangeName = "chat.exchange", string routingPattern = "chat.#")
    {
        if (IsConnected) return;
        _channel = await _connection.CreateChannelAsync();
        await _channel.ExchangeDeclareAsync(exchangeName, "topic", durable: true, autoDelete: false, arguments: null);
        var qOk = await _channel.QueueDeclareAsync($"chat.client.{Guid.NewGuid():N}", durable: false, exclusive: true, autoDelete: true, arguments: null);
        _queueName = qOk.QueueName;
        await _channel.QueueBindAsync(_queueName, exchangeName, routingPattern, arguments: null);
        await StartConsumerAsync(_queueName);
        IsConnected = true;
        SystemEvent?.Invoke("AMQP CONNECTED");
    }

    private async Task StartConsumerAsync(string queue)
    {
        if (_channel is null) return;
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var msg = JsonSerializer.Deserialize<ChatMessage>(json);
                // Clean minimal implementation for RabbitMQ.Client v7
                using System.Text;
                using System.Text.Json;
                using RabbitMQ.Client;
                using RabbitMQ.Client.Events;

                namespace MessagingAspire.Web.Services;

                public class AmqpChatClient : IAsyncDisposable
                {
                    private readonly IConnection _connection;
                    private IChannel? _channel;
                    private string? _queueName;
                    public event Action<ChatMessage>? MessageReceived;
                    public event Action<string>? SystemEvent;
                    public bool IsConnected { get; private set; }

                    public AmqpChatClient(IConnection connection) => _connection = connection;

                    public async Task ConnectAsync(string userDisplayName, string exchangeName = "chat.exchange", string routingPattern = "chat.#")
                    {
                        if (IsConnected) return;
                        _channel = await _connection.CreateChannelAsync();
                        await _channel.ExchangeDeclareAsync(exchangeName, "topic", durable: true, autoDelete: false, arguments: null);
                        var qOk = await _channel.QueueDeclareAsync($"chat.client.{Guid.NewGuid():N}", durable: false, exclusive: true, autoDelete: true, arguments: null);
                        _queueName = qOk.QueueName;
                        await _channel.QueueBindAsync(_queueName, exchangeName, routingPattern, arguments: null);
                        await StartConsumerAsync(_queueName);
                        IsConnected = true;
                        SystemEvent?.Invoke("AMQP CONNECTED");
            if (result is Task t) t.GetAwaiter().GetResult();
                    private async Task StartConsumerAsync(string queue)
                    {
                        if (_channel is null) return;
                        var consumer = new AsyncEventingBasicConsumer(_channel);
                        consumer.Received += async (_, ea) =>
                        {
                            try
                            {
                                var body = ea.Body.ToArray();
                                var json = Encoding.UTF8.GetString(body);
                                var msg = JsonSerializer.Deserialize<ChatMessage>(json);
                                if (msg != null) MessageReceived?.Invoke(msg);
                            }
                            catch (Exception ex)
                            {
                                SystemEvent?.Invoke("Failed to parse message: " + ex.Message);
                            }
                            await Task.CompletedTask;
                        };
                        await _channel.BasicConsumeAsync(queue: queue, autoAck: true, consumer: consumer);
                        SystemEvent?.Invoke($"Subscribed {queue}");
                    }

                    public async Task SendChatMessageAsync(string user, string text, string exchangeName = "chat.exchange", string routingKey = "chat.message")
                    {
                        if (!IsConnected || _channel is null) return;
                        var chat = new ChatMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            User = user,
                            Text = text,
                            Timestamp = DateTime.UtcNow
                        };
                        var json = JsonSerializer.Serialize(chat);
                        var body = Encoding.UTF8.GetBytes(json);
                        var props = new BasicProperties
                        {
                            ContentType = "application/json",
                            DeliveryMode = DeliveryModes.Transient
                        };
                        await _channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, mandatory: false, basicProperties: props, body: body);
                    }

                    public async Task DisconnectAsync()
                    {
                        if (!IsConnected) return;
                        try
                        {
                            if (_channel is not null)
                            {
                                await _channel.CloseAsync();
                                _channel.Dispose();
                            }
                        }
                        catch { }
                        finally
                        {
                            _channel = null;
                            IsConnected = false;
                        }
                        SystemEvent?.Invoke("Disconnected");
                    }

                    public async ValueTask DisposeAsync()
                    {
                        await DisconnectAsync();
                    }
                }
                }
            }
            catch (Exception ex)
            {
                SystemEvent?.Invoke("Failed to parse message: " + ex.Message);
            }
        };
        _channel.BasicConsume(queue: queue, autoAck: true, consumer: consumer);
        SystemEvent?.Invoke($"Subscribed {queue}");
    }

    public Task SendChatMessageAsync(string user, string text, string exchangeName = "chat.exchange", string routingKey = "chat.message")
    {
        if (!IsConnected || _channel == null) return Task.CompletedTask;
        var chat = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            User = user,
            Text = text,
            Timestamp = DateTime.UtcNow
        };
        var json = JsonSerializer.Serialize(chat);
        var body = Encoding.UTF8.GetBytes(json);
        var props = _channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 1; // non-persistent
        _channel.BasicPublish(exchange: exchangeName, routingKey: routingKey, mandatory: false, basicProperties: props, body: body);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        if (!IsConnected) return Task.CompletedTask;
        try
        {
            _channel?.Close();
            _channel?.Dispose();
        }
        catch { }
        finally
        {
            _channel = null;
            IsConnected = false;
        }
        SystemEvent?.Invoke("Disconnected");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisconnectAsync();
        return ValueTask.CompletedTask;
    }
}
