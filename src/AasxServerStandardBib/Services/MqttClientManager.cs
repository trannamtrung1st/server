namespace AasxServerStandardBib.Services;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;

public class MqttClientManager : IDisposable
{
    private readonly IMqttClient _publisher;
    private readonly ConcurrentDictionary<string, IMqttClient> _subscribers;

    public MqttClientManager()
    {
        //create MQTT Client and Connect using options above
        _publisher = new MqttFactory().CreateMqttClient();
        _subscribers = new();
    }

    public void Dispose()
    {
        using var _1 = _publisher;
        foreach (var s in _subscribers.Values)
        {
            s.Dispose();
        }
    }

    public async Task<IMqttClient> GetPublisher()
    {
        await TryConnect(_publisher, "Event Publisher");
        return _publisher;
    }

    public async Task<IMqttClient> GetSubscriber(string key)
    {
        var subscriber = _subscribers.GetOrAdd(key, _ => new MqttFactory().CreateMqttClient());
        await TryConnect(subscriber, key);
        return subscriber;
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