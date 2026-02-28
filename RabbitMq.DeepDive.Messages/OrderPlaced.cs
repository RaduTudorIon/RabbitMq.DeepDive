namespace RabbitMq.DeepDive.Messages;

public record OrderPlaced(
    Guid OrderId,
    string ProductName,
    int Quantity,
    decimal Price,
    DateTimeOffset PlacedAt);
