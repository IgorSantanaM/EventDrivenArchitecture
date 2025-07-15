using EDA.Consumer.Core.ExternalEvents;

namespace EDA.Consumer.Core
{
    public class OrderCompletedEventHandler(ILogger<OrderCompletedEventHandler> logger)
    {
        public async Task Handle(OrderCompletedEvent orderCompletedEvent)
        {
            logger.LogInformation($"Order {orderCompletedEvent.OrderId} has been completed.");

            if (orderCompletedEvent.OrderId.StartsWith("6"))
            {
                throw new Exception("Invalid Event");
            }
        }
    }
}
