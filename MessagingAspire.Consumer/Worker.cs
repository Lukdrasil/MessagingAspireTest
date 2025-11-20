using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessagingAspire.Consumer;

public class Worker(ILogger<Worker> logger, IConnection connection) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting RabbitMQ Consumer...");

        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declare exchange and queue (same as producer)
        await channel.ExchangeDeclareAsync(
            exchange: "chat.exchange",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: "chat.queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: "chat.queue",
            exchange: "chat.exchange",
            routingKey: "chat.#",
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                logger.LogInformation("Received message: {Message}", message);
                
                // Acknowledge the message
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message");
            }
        };

        await channel.BasicConsumeAsync(
            queue: "chat.queue",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("Consumer is now listening for messages...");

        // Keep the worker running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}
