using RabbitMq.DeepDive.Messages;
using Wolverine;

namespace RabbitMq.DeepDive.Producer;

public class OrderPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<OrderPublisherService> logger;

    public OrderPublisherService(IServiceScopeFactory scopeFactory, ILogger<OrderPublisherService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            var order = new OrderPlaced(
                Guid.NewGuid(),
                $"Product-{Random.Shared.Next(1, 100)}",
                Random.Shared.Next(1, 10),
                Math.Round((decimal)(Random.Shared.NextDouble() * 490 + 10), 2),
                DateTimeOffset.UtcNow);

            await bus.PublishAsync(order);

            logger.LogInformation(
                "Published order {OrderId}: {ProductName} x{Quantity} @ {Price:C}",
                order.OrderId, order.ProductName, order.Quantity, order.Price);

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
