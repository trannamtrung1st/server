namespace AasxServerStandardBib.Services;

using System;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;

public class MqttClientManager : IDisposable
{
    private readonly IMqttClient _publisher;
    private readonly IMqttClient _subscriber;

    public MqttClientManager()
    {
        //create MQTT Client and Connect using options above
        _publisher = new MqttFactory().CreateMqttClient();
        _subscriber = new MqttFactory().CreateMqttClient();
    }

    public void Dispose()
    {
        using var _1 = _publisher;
        using var _2 = _subscriber;
    }

    public async Task<IMqttClient> GetPublisher()
    {
        await TryConnect(_publisher, "Event Publisher");
        return _publisher;
    }

    public async Task<IMqttClient> GetSubscriber()
    {
        await TryConnect(_subscriber, "Event Subscriber");
        return _subscriber;
    }

    private static async Task TryConnect(IMqttClient mqttClient, string clientId)
    {
        if (!mqttClient.IsConnected)
        {
            // Create TCP based options using the builder.
            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer("localhost", 1883)
                .Build();

            _ = await mqttClient.ConnectAsync(options);
        }
    }
}