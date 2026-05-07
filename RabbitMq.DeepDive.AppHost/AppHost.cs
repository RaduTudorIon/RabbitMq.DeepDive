var builder = DistributedApplication.CreateBuilder(args);

var configPath = Path.Combine(builder.AppHostDirectory, "rabbitmq-config");
var enabledPluginsPath = Path.Combine(configPath, "enabled_plugins");
Console.WriteLine($"RabbitMQ config path: {configPath}");

var rabbitUser = builder.AddParameter("rabbitmq-health-user", "guest");
var rabbitPassword = builder.AddParameter("rabbitmq-health-password", "guest", secret: true);

var rabbit = builder.AddRabbitMQ("rabbitmq", userName: rabbitUser, password: rabbitPassword)
    .WithManagementPlugin()
    .WithBindMount(configPath, "/etc/rabbitmq/conf.d")
    .WithBindMount(enabledPluginsPath, "/etc/rabbitmq/enabled_plugins");

var apiService = builder.AddProject<Projects.RabbitMq_DeepDive_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbit)
    .WithEnvironment("ConnectionStrings__rabbitmq-management", rabbit.GetEndpoint("management"))
    .WaitFor(rabbit);

builder.AddProject<Projects.RabbitMq_DeepDive_Producer>("producer")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbit)
    .WaitFor(rabbit);

builder.AddProject<Projects.RabbitMq_DeepDive_Consumer>("consumer")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbit)
    .WaitFor(rabbit);

builder.Build().Run();
