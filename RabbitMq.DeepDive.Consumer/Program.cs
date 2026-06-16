using RabbitMQ.Client;
using Scalar.AspNetCore;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

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

    _ = opts.UseRabbitMq(rabbit =>
    {
        rabbit.HostName = host;
        rabbit.Port = port;
        rabbit.UserName = "consumer";
        rabbit.Password = "changeme";
        rabbit.VirtualHost = "TestVhost";
    })
    .AutoProvision()
    .UseQuorumQueues()
    .DeclareExchange("TestDirect.Exch", e =>
    {
        e.ExchangeType = Wolverine.RabbitMQ.ExchangeType.Direct;
        e.IsDurable = true;
    })
    .DeclareExchange("TestFanout.Exch", e =>
    {
        e.ExchangeType = Wolverine.RabbitMQ.ExchangeType.Fanout;
        e.IsDurable = true;
    })
    .DeclareExchange("TestTopic.Exch", e =>
    {
        e.ExchangeType = Wolverine.RabbitMQ.ExchangeType.Topic;
        e.IsDurable = true;
    })
    .DeclareExchange("Orders.Exch", e =>
    {
        e.ExchangeType = Wolverine.RabbitMQ.ExchangeType.Fanout;
        e.IsDurable = true;
    })
    .DeclareQueue("DirectRoutingTest.Q")
    .DeclareQueue("DirectRoutingAdces.Q")
    .DeclareQueue("TestFanout.Q")
    .DeclareQueue("TestFanout1.Q")
    .DeclareQueue("TestFanout2.Q")
    .DeclareQueue("TestFanout3.Q")
    .DeclareQueue("TopicRoutingTest.Q")
    .DeclareQueue("TopicRoutingAdces.Q")
    .DeclareQueue("Orders.ErrorQ")
    .DeclareQueue("ShovelToCloud.Q")
    .BindExchange("TestDirect.Exch").ToQueue("DirectRoutingTest.Q", "test")
    .BindExchange("TestDirect.Exch").ToQueue("DirectRoutingAdces.Q", "adces")
    .BindExchange("TestFanout.Exch").ToQueue("TestFanout.Q", "")
    .BindExchange("TestFanout.Exch").ToQueue("TestFanout1.Q", "")
    .BindExchange("TestFanout.Exch").ToQueue("TestFanout2.Q", "")
    .BindExchange("TestFanout.Exch").ToQueue("TestFanout3.Q", "")
    .BindExchange("TestTopic.Exch").ToQueue("TopicRoutingTest.Q", "rabit.*")
    .BindExchange("TestTopic.Exch").ToQueue("TopicRoutingAdces.Q", "*.adces.#")

    .BindExchange("Orders.Exch").ToQueue("Orders.Q");

    opts.Policies.OnAnyException()
        .RetryWithCooldown(
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5));

    opts.ListenToRabbitQueue("Orders.Q")
        .PreFetchCount(20)
        .MaximumParallelMessages(4)
        .CircuitBreaker(cb =>
        {
            cb.MinimumThreshold = 10;
            cb.FailurePercentageThreshold = 20;
            cb.PauseTime = TimeSpan.FromSeconds(30);
            cb.TrackingPeriod = TimeSpan.FromMinutes(2);
        })
        .DeadLetterQueueing(new DeadLetterQueue("Orders.ErrorQ", DeadLetterQueueMode.InteropFriendly));
});

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();

app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Consumer API")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// Redirect root to Scalar UI
app.MapGet("/", () => Results.Redirect("/scalar/v1"))
    .ExcludeFromDescription();

app.MapDefaultEndpoints();

app.Run();