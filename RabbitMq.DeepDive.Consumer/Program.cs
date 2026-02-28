using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

builder.Host.UseWolverine(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("rabbitmq");

    var uri = new Uri(connectionString!);
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5672;

    opts.UseRabbitMq(rabbit =>
    {
        rabbit.HostName = host;
        rabbit.Port = port;
        rabbit.UserName = "consumer";
        rabbit.Password = "changeme";
        rabbit.VirtualHost = "/";
    })
    .AutoProvision();

    opts.ListenToRabbitQueue("orders");
});

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();

app.Run();
