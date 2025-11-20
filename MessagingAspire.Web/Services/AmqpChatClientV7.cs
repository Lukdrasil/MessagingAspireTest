using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessagingAspire.Web.Services;

public class AmqpChatClientV7 : IAsyncDisposable
{
    private readonly IConnection _connection;
    private IChannel? _channel;
    private string? _queueName;
    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? SystemEvent;
    public bool IsConnected { get; private set; }

    public AmqpChatClientV7(IConnection connection) => _connection = connection;

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
        var consumer = new ChatConsumer(_channel, m => MessageReceived?.Invoke(m), info => SystemEvent?.Invoke(info));
        await _channel.BasicConsumeAsync(queue: queue,
                                         autoAck: true,
                                         consumerTag: $"chat-consumer-{Guid.NewGuid():N}",
                                         noLocal: false,
                                         exclusive: true,
                                         arguments: null,
                                         consumer: consumer,
                                         cancellationToken: CancellationToken.None);
        SystemEvent?.Invoke($"Subscribed {queue}");
    }

    private sealed class ChatConsumer : IAsyncBasicConsumer
    {
        public IChannel Channel { get; }
        private readonly Action<ChatMessage> _onMessage;
        private readonly Action<string> _onSystem;
        public ChatConsumer(IChannel channel, Action<ChatMessage> onMessage, Action<string> onSystem)
        {
            Channel = channel;
            _onMessage = onMessage;
            _onSystem = onSystem;
        }
        public Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            try
            {
                var json = Encoding.UTF8.GetString(body.Span);
                var msg = JsonSerializer.Deserialize<ChatMessage>(json);
                if (msg != null) _onMessage(msg);
            }
            catch (Exception ex)
            {
                _onSystem("Failed to parse message: " + ex.Message);
            }
            return Task.CompletedTask;
        }
        public Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason) => Task.CompletedTask;
        public Task HandleBasicRecoverOkAsync(string consumerTag, CancellationToken cancellationToken) => Task.CompletedTask;
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
