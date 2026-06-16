using Microsoft.EntityFrameworkCore;
using RabbitMq.DeepDive.Messages;
using RabbitMq.DeepDive.Producer;
using RabbitMQ.Client.Exceptions;
using Scalar.AspNetCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var pgConnectionString = builder.Configuration.GetConnectionString("outboxdb")
    ?? "Host=localhost;Database=outboxdb;Username=admin;Password=changeme";

builder.Host.UseWolverine(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("rabbitmq")
        ?? "amqp://guest:guest@localhost:5672";

    var uri = new Uri(connectionString);
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5672;

    opts.UseRabbitMq(rabbit =>
    {
        rabbit.HostName = host;
        rabbit.Port = port;
        rabbit.UserName = "producer";
        rabbit.Password = "changeme";
        rabbit.VirtualHost = "TestVhost";
    })
    .DisableDeadLetterQueueing()
    .UseQuorumQueues()
    .AutoProvision();

    // Wolverine stores outbox envelopes in PostgreSQL, then relays them to
    // RabbitMQ after the DB transaction commits- at-least-once delivery.
    opts.PersistMessagesWithPostgresql(pgConnectionString, schemaName: "wolverine");
    opts.UseEntityFrameworkCoreTransactions();

    opts.PublishMessage<OrderPlaced>()
        .ToRabbitExchange("Orders.Exch")
        .UseDurableOutbox()
        .DeliverWithin(TimeSpan.FromMinutes(2))
        .CircuitBreaking(circuit =>
        {
            circuit.FailuresBeforeCircuitBreaks = 5;
            circuit.PingIntervalForCircuitResume = TimeSpan.FromSeconds(30);
            circuit.MaximumEnvelopeRetryStorage = 200;
        })
        .ConfigureSending(failures =>
        {
            failures.OnException<BrokerUnreachableException>()
                .PauseSending(TimeSpan.FromSeconds(15));

            failures.OnAnyException()
                .RetryWithCooldown(
                    TimeSpan.FromMilliseconds(250),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5));
        });
});

// Registers OutboxDbContext and hooks EF Core's SaveChanges pipeline into
// Wolverine's outbox so the business row and the message envelope are committed atomically.
builder.Services.AddDbContextWithWolverineIntegration<OutboxDbContext>(opts =>
    opts.UseNpgsql(pgConnectionString));

builder.Services.AddHostedService<OrderPublisherService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Producer API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Redirect root to Scalar UI
app.MapGet("/", () => Results.Redirect("/scalar/v1"))
    .ExcludeFromDescription();

app.MapDefaultEndpoints();

app.MapPost("/orders", async (CreateOrderRequest request, IMessageBus bus) =>
{
    var order = new OrderPlaced(
        request.OrderId ?? Guid.NewGuid(),
        request.ProductName ?? "Manual Order",
        request.Quantity ?? 1,
        request.Price ?? 99.99m,
        DateTimeOffset.UtcNow);

    await bus.PublishAsync(order);
    return Results.Accepted($"/orders/{order.OrderId}", order);
})
.WithName("PublishOrder")
.WithSummary("Publish a new order message")
.WithDescription("Publishes a new order message to the RabbitMQ queue for processing")
.WithOpenApi();

app.MapPost("/orders/outbox", async (CreateOrderRequest request, IDbContextOutbox<OutboxDbContext> outbox) =>
{
    var entity = new OutboxOrder
    {
        Id = request.OrderId ?? Guid.NewGuid(),
        ProductName = request.ProductName ?? "Outbox Order",
        Quantity = request.Quantity ?? 1,
        Price = request.Price ?? 99.99m,
        PlacedAt = DateTimeOffset.UtcNow
    };

    // Wolverine outbox: the OutboxOrder row and the OrderPlaced envelope are
    // written to PostgreSQL in a single transaction. Wolverine's background relay
    // forwards the envelope to RabbitMQ after commit — guaranteed at-least-once
    // delivery even if the process crashes between the DB write and the broker ack.
    outbox.DbContext.OutboxOrders.Add(entity);
    await outbox.PublishAsync(new OrderPlaced(
        entity.Id,
        entity.ProductName,
        entity.Quantity,
        entity.Price,
        entity.PlacedAt));

    await outbox.SaveChangesAndFlushMessagesAsync();

    return Results.Created($"/orders/{entity.Id}", new
    {
        entity.Id,
        entity.ProductName,
        entity.Quantity,
        entity.Price,
        entity.PlacedAt,
        outboxTable = "PostgreSQL / wolverine.wolverine_outgoing_envelopes"
    });
})
.WithName("PublishOrderViaOutbox")
.WithSummary("Publish an order using the Wolverine transactional outbox")
.WithDescription(
    "Demonstrates the transactional outbox pattern: the order row and the OrderPlaced envelope " +
    "are written to PostgreSQL in a single transaction by Wolverine. A background relay then " +
    "forwards the envelope to RabbitMQ — guaranteeing at-least-once delivery even if the app " +
    "crashes between the DB commit and the broker publish.")
.WithOpenApi();

app.MapPost("/orders/fail", async (IMessageBus bus) =>
{
    var order = new OrderPlaced(
        Guid.NewGuid(),
        "Invalid Order",
        0,
        99.99m,
        DateTimeOffset.UtcNow);

    await bus.PublishAsync(order);
    return Results.Accepted($"/orders/{order.OrderId}", order);
})
.WithName("PublishFailingOrder")
.WithSummary("Publish an order that will be dead-lettered")
.WithDescription("Publishes an order with Quantity=0, which throws InvalidOperationException in the handler and routes the message to Orders.dlq")
.WithOpenApi();

app.Run();

record CreateOrderRequest(
    Guid? OrderId = null,
    string? ProductName = null,
    int? Quantity = null,
    decimal? Price = null);
