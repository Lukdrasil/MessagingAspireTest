using MessagingAspire.Consumer;

var builder = Host.CreateApplicationBuilder(args);

// Add RabbitMQ client
builder.AddRabbitMQClient("rabbitmq");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
