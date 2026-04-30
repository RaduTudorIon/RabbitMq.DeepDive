using RabbitMQ.Client;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

var rabbitConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
    ?? "amqp://guest:guest@localhost:5672";

builder.Services.AddHealthChecks()
    .AddRabbitMQ(sp =>
    {
        var uri = new Uri(rabbitConnectionString);
        var factory = new ConnectionFactory
        {
            HostName = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5672,
            UserName = "consumer",
            Password = "changeme",
            VirtualHost = "TestVhost"
        };
        return factory.CreateConnectionAsync();
    }, name: "rabbitmq", tags: ["ready"]);

builder.Host.UseWolverine(opts =>
{
    var uri = new Uri(rabbitConnectionString);
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5672;

    opts.UseRabbitMq(rabbit =>
    {
        rabbit.HostName = host;
        rabbit.Port = port;
        rabbit.UserName = "consumer";
        rabbit.Password = "changeme";
        rabbit.VirtualHost = "TestVhost";
    })
    .AutoProvision();

    opts.ListenToRabbitQueue("orders");
});

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();

app.Run();
