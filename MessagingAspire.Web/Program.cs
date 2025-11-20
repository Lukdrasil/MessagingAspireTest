using MessagingAspire.Web;
using MessagingAspire.Web.Components;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add RabbitMQ client
builder.AddRabbitMQClient("rabbitmq");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// AMQP chat client using RabbitMQ.Client v7 via Aspire connection
builder.Services.AddSingleton<MessagingAspire.Web.Services.AmqpChatClientV7>();

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

// Add HttpClient for internal API calls
builder.Services.AddHttpClient("LocalApi", client =>
{
    // This will be set at runtime to the current request base address
});
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return httpClientFactory.CreateClient("LocalApi");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// API endpoint to get RabbitMQ WebSocket URL + credentials (DEV ONLY: exposes password)
app.MapGet("/api/rabbitmq-config", (IConfiguration configuration) =>
{
    var connectionString = configuration.GetConnectionString("rabbitmq");
    // Default to configured user (chatuser/chatpass) if connection string missing
    string login = "chatuser";
    string passcode = "chatpass";
    string stompUrl = "ws://localhost:15674/ws";

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        try
        {
            var uri = new Uri(connectionString);
            var host = uri.Host;
            var parts = uri.UserInfo.Split(':');
            if (parts.Length == 2)
            {
                login = parts[0];
                passcode = parts[1];
            }
            // For local dev map container host to localhost so browser can reach it
            stompUrl = $"ws://{(host == "rabbitmq" ? "localhost" : host)}:15674/ws";
        }
        catch { /* keep defaults */ }
    }

    return Results.Json(new { stompUrl, login, passcode });
}).WithName("GetRabbitMQConfig");

app.MapDefaultEndpoints();

app.Run();
