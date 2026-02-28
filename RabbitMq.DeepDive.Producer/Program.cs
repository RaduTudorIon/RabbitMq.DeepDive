using RabbitMq.DeepDive.Messages;
using RabbitMq.DeepDive.Producer;
using Scalar.AspNetCore;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

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
        rabbit.VirtualHost = "/";
    })
    .AutoProvision();

    opts.PublishMessage<OrderPlaced>().ToRabbitQueue("orders");
});

builder.Services.AddHostedService<OrderPublisherService>();

var app = builder.Build();

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

app.MapPost("/orders", async (IMessageBus bus) =>
{
    var order = new OrderPlaced(
        Guid.NewGuid(),
        "Manual Order",
        1,
        99.99m,
        DateTimeOffset.UtcNow);

    await bus.PublishAsync(order);
    return Results.Accepted($"/orders/{order.OrderId}", order);
})
.WithName("PublishOrder")
.WithSummary("Publish a new order message")
.WithDescription("Publishes a new order message to the RabbitMQ queue for processing")
.WithOpenApi();

app.Run();
