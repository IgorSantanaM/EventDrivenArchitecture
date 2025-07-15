using EDA.Producer.Core;
using System.Text.Json;

namespace EDA.Producer.Adapters;

public class PostgresOrders(OrdersDbContext context) : IOrders
{
    public async Task<Order> New(string customerId)
    {
        using var transaction = await context.Database.BeginTransactionAsync();
        var order = new Order()
        {
            CustomerId = customerId,
            OrderId = Guid.NewGuid().ToString(),
            Completed = false
        };

        await context.Orders.AddAsync(order);
        await context.Outbox.AddAsync(new OutboxItem()
        {
            EventTime = DateTime.Now,
            Processed = false,
            EventData = JsonSerializer.Serialize(new OrderCreatedEvent() { OrderId = order.OrderId }),
            EventType = nameof(OrderCreatedEvent),
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return order;
    }

    public async Task<Order> Complete(string orderId)
    {
        using var transaction = await context.Database.BeginTransactionAsync();

        Order? order = await context.Orders.FindAsync(orderId);
        if (order == null)
        {
            throw new InvalidOperationException("Order not found");
        }
        order.Completed = true;
        await context.Outbox.AddAsync(new OutboxItem()
        {
            EventTime = DateTime.Now,
            Processed = false,
            EventData = JsonSerializer.Serialize(new OrderCreatedEvent() { OrderId = order.OrderId }),
            EventType = nameof(OrderCreatedEvent),
        });


        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return order;
    }
}