using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add RabbitMQ client
builder.AddRabbitMQClient("rabbitmq");

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// Endpoint for sending messages to RabbitMQ
app.MapPost("/messages", async (MessageRequest request, IConnection connection) =>
{
    try
    {
        await using var channel = await connection.CreateChannelAsync();
        
        // Declare exchange and queue
        await channel.ExchangeDeclareAsync(
            exchange: "chat.exchange",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        await channel.QueueDeclareAsync(
            queue: "chat.queue",
            durable: true,
            exclusive: false,
            autoDelete: false);

        await channel.QueueBindAsync(
            queue: "chat.queue",
            exchange: "chat.exchange",
            routingKey: "chat.#");

        var message = new
        {
            Id = Guid.NewGuid(),
            User = request.User ?? "Anonymous",
            Text = request.Text,
            Timestamp = DateTime.UtcNow
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await channel.BasicPublishAsync(
            exchange: "chat.exchange",
            routingKey: "chat.message",
            body: body);

        return Results.Ok(new { success = true, message });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to send message: {ex.Message}");
    }
})
.WithName("SendMessage");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record MessageRequest(string? User, string Text);
