using CloudNative.CloudEvents.SystemTextJson;
using CloudNative.CloudEvents;
using EDA.Consumer.Adapters;
using EDA.Consumer.Core;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.Mime;
using EDA.Consumer.Core.ExternalEvents;

namespace EDA.Consumer
{
    public class OrderCompletedEventWorker(OrderCompletedEventHandler handler, IOptions<RabbitMqSettings> settings, ILogger<OrderCompletedEventWorker> logger, RabbitMQConnection connection) : BackgroundService
    {
        const string QUEUE_NAME = "consumer-order-completed";
        const string DEAD_LETTER_QUEUE_NAME = "consumer-order-completed-dlq";
        private const string ROUTING_KEY = "order-created";
        private IChannel orderCreatedChannel;
        private HashSet<string> _processedEventIds = new();

        private async Task HandleEvent(object model, BasicDeliverEventArgs ea)
        {
            var deliveryCount = 0;

            if (ea.BasicProperties.Headers != null && ea.BasicProperties.Headers["x-delivery-count"] != null)
            {
                deliveryCount = int.Parse(ea.BasicProperties.Headers["x-delivery-count"].ToString());
            }

            logger.LogInformation("Delivery count is {deliveryCount}", deliveryCount);

            try
            {
                var body = ea.Body.ToArray();
                var formatter = new JsonEventFormatter<OrderCompletedEvent>();
                var evtWrapper = await formatter.DecodeStructuredModeMessageAsync(new MemoryStream(body), new ContentType("application/json"), new List<CloudEventAttribute>(0));

                if (_processedEventIds.Contains(evtWrapper.Id))
                {
                    logger.LogInformation("Event already processed. Skipping.");
                    await orderCreatedChannel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                await handler.Handle(evtWrapper.Data as OrderCompletedEvent);

                await orderCreatedChannel.BasicAckAsync(ea.DeliveryTag, false);
                _processedEventIds.Add(evtWrapper.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $" [x] Failure processing message. {ex.Message}");

                await orderCreatedChannel.BasicRejectAsync(ea.DeliveryTag, deliveryCount < 3);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var configuredChannel = await connection.SetupConsumerFor(new ChannelConfig(settings.Value.ExchangeName, QUEUE_NAME, ROUTING_KEY, settings.Value.DeadLetterExchangeName, DEAD_LETTER_QUEUE_NAME, HandleEvent), stoppingToken);
            orderCreatedChannel = configuredChannel.Channel;

            while (!stoppingToken.IsCancellationRequested)
            {
                await orderCreatedChannel.BasicConsumeAsync(QUEUE_NAME, false, configuredChannel.Consumer, cancellationToken: stoppingToken);

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
