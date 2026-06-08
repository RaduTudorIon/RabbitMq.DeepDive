var builder = DistributedApplication.CreateBuilder(args);

var configPath = Path.Combine(builder.AppHostDirectory, "rabbitmq-config");
var enabledPluginsPath = Path.Combine(configPath, "enabled_plugins");
var prometheusConfigPath = Path.Combine(builder.AppHostDirectory, "prometheus");
var grafanaProvisioningPath = Path.Combine(builder.AppHostDirectory, "grafana", "provisioning");
var grafanaDashboardsPath = Path.Combine(builder.AppHostDirectory, "grafana", "dashboards");
Console.WriteLine($"RabbitMQ config path: {configPath}");

var rabbitUser = builder.AddParameter("rabbitmq-health-user", "guest");
var rabbitPassword = builder.AddParameter("rabbitmq-health-password", "guest", secret: true);

var rabbit = builder.AddRabbitMQ("rabbitmq", userName: rabbitUser, password: rabbitPassword, port: 5672)
    .WithManagementPlugin(port: 15672)
    .WithBindMount(configPath, "/etc/rabbitmq/conf.d")
    .WithBindMount(enabledPluginsPath, "/etc/rabbitmq/enabled_plugins");

var rabbitMqExporter = builder.AddContainer("rabbitmq-exporter", "kbudde/rabbitmq-exporter", "latest")
    .WithHttpEndpoint(targetPort: 9419, name: "http")
    .WithEnvironment("RABBIT_URL", "http://rabbitmq:15672")
    .WithEnvironment("RABBIT_USER", "guest")
    .WithEnvironment("RABBIT_PASSWORD", rabbitPassword)
    .WithEnvironment("PUBLISH_PORT", "9419")
    .WaitFor(rabbit);

var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "v2.55.1")
    .WithHttpEndpoint(targetPort: 9090, name: "http")
    .WithBindMount(prometheusConfigPath, "/etc/prometheus")
    .WaitFor(rabbitMqExporter);

var grafana = builder.AddContainer("grafana", "grafana/grafana-oss", "11.2.0")
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Viewer")
    .WithBindMount(grafanaProvisioningPath, "/etc/grafana/provisioning")
    .WithBindMount(grafanaDashboardsPath, "/etc/grafana/provisioning/dashboards/json")
    .WaitFor(prometheus);

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
