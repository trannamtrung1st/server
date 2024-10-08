namespace AasxServerStandardBib.Services;

using System.Text.Json;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

public class EventPublisher(MqttClientManager mqttClientManager)
{
    public async Task Publish(string topic, object payload)
    {
        var publisher = await mqttClientManager.GetPublisher();

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(payload))
            .WithExactlyOnceQoS()
            .Build();

        _ = await publisher.PublishAsync(message);
    }
}