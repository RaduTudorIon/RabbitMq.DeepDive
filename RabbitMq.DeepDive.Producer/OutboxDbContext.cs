using Microsoft.EntityFrameworkCore;

namespace RabbitMq.DeepDive.Producer;

public class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : DbContext(options)
{
    public DbSet<OutboxOrder> OutboxOrders => Set<OutboxOrder>();
}

public class OutboxOrder
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public string Status { get; set; } = "Submitted";
}
