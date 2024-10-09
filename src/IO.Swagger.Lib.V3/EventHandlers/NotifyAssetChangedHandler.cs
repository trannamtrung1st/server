namespace IO.Swagger.Lib.V3.EventHandlers;

using AasxServerStandardBib;
using System;
using System.Threading.Tasks;
using AasxServerStandardBib.Services;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Client;
using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;
using AasxServerStandardBib.EventHandlers.Abstracts;
using System.Text;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using IO.Swagger.Lib.V3.Services;

public class NotifyAssetChangedHandler(
    MqttClientManager mqttClientManager,
    IServiceProvider serviceProvider,
    ILogger<NotifyAssetChangedHandler> logger) : IEventHandler
{
    public async Task Start()
    {
        var subscriber = await mqttClientManager.GetSubscriber(nameof(NotifyAssetChangedHandler));

        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(AasEvents.AasUpdated, MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        _ = await subscriber.SubscribeAsync(options, cancellationToken: default);

        _ = subscriber.UseApplicationMessageReceivedHandler(async (e) =>
        {
            try
            {
                await Handle(e);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        });
    }

    private async Task Handle(MQTTnet.MqttApplicationMessageReceivedEventArgs eArgs)
    {
        var json = Encoding.UTF8.GetString(eArgs.ApplicationMessage.Payload);
        var jToken = JToken.Parse(json);
        var aasId = (jToken as JValue).Value<string>();
        using var scope = serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
        await notificationService.NotifyAssetChanged(Guid.Parse(aasId));
    }
}