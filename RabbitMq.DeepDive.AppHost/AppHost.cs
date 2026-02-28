var builder = DistributedApplication.CreateBuilder(args);

var configPath = Path.Combine(builder.AppHostDirectory, "rabbitmq-config");
Console.WriteLine($"RabbitMQ config path: {configPath}");

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithBindMount(configPath, "/etc/rabbitmq/conf.d");

var apiService = builder.AddProject<Projects.RabbitMq_DeepDive_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbit)
    .WithEnvironment("ConnectionStrings__rabbitmq-management", rabbit.GetEndpoint("management"))
    .WaitFor(rabbit);

builder.AddProject<Projects.RabbitMq_DeepDive_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.RabbitMq_DeepDive_Producer>("producer")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbit)
    .WaitFor(rabbit);

builder.AddProject<Projects.RabbitMq_DeepDive_Consumer>("consumer")
    .WithHttpHealthCheck("/health")
    .WithReference(rabbit)
    .WaitFor(rabbit);

builder.Build().Run();
