using RabbitMq.DeepDive.Messages;

namespace RabbitMq.DeepDive.Consumer;

public class OrderPlacedHandler(ILogger<OrderPlacedHandler> logger)
{
    public void Handle(OrderPlaced message)
    {
        logger.LogInformation(
            "Order received: {OrderId} | {ProductName} x{Quantity} @ {Price:C} | Placed: {PlacedAt}",
            message.OrderId,
            message.ProductName,
            message.Quantity,
            message.Price,
            message.PlacedAt);
    }
}