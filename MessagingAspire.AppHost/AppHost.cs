var builder = DistributedApplication.CreateBuilder(args);

// Add RabbitMQ with STOMP plugin enabled
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithDataVolume()
    .WithEnvironment("RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS", "-rabbitmq_management path_prefix \"/rabbitmq\"")
    // Use enabled_plugins file to activate STOMP + Web STOMP
    .WithBindMount("./rabbitmq/enabled_plugins", "/etc/rabbitmq/enabled_plugins")
    .WithEnvironment("RABBITMQ_ENABLED_PLUGINS_FILE", "/etc/rabbitmq/enabled_plugins")
    // Define a non-guest default user to allow remote container access
    .WithEnvironment("RABBITMQ_DEFAULT_USER", "chatuser")
    .WithEnvironment("RABBITMQ_DEFAULT_PASS", "chatpass")
    // (Optional) explicitly define STOMP/Web STOMP ports via env (defaults are usually fine)
    .WithEnvironment("RABBITMQ_STOMP_TCP_PORT", "61613")
    .WithEnvironment("RABBITMQ_WEB_STOMP_TCP_PORT", "15674")
    // Publish STOMP WebSocket and raw STOMP TCP ports
    .WithEndpoint(name: "stomp-ws", port: 15674, targetPort: 15674, scheme: "ws")
    .WithEndpoint(name: "stomp-tcp", port: 61613, targetPort: 61613, scheme: "tcp");

//var apiService = builder.AddProject<Projects.MessagingAspire_ApiService>("apiservice")
//    .WithHttpHealthCheck("/health")
//    .WithReference(rabbitmq);

// Add Consumer worker service
builder.AddProject<Projects.MessagingAspire_Consumer>("consumer")
    .WithReference(rabbitmq);

builder.AddProject<Projects.MessagingAspire_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    //.WithReference(apiService)
    .WithReference(rabbitmq);
    //.WaitFor(apiService);

builder.Build().Run();
