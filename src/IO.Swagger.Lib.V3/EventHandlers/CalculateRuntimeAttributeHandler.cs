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
using AasxServer;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using AasxServerStandardBib.Models;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using IO.Swagger.Controllers;
using AasxServerStandardBib.Utils;
using IO.Swagger.Models;
using Microsoft.AspNetCore.Mvc;
using Extensions;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using IO.Swagger.Lib.V3.Services;

public class CalculateRuntimeAttributeHandler(
    MqttClientManager mqttClientManager,
    IServiceProvider serviceProvider,
    EventPublisher eventPublisher,
    ILogger<CalculateRuntimeAttributeHandler> logger) : IEventHandler
{
    public async Task Start()
    {
        var subscriber = await mqttClientManager.GetSubscriber(nameof(CalculateRuntimeAttributeHandler));

        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(AasEvents.SubmodelElementUpdated, MqttQualityOfServiceLevel.ExactlyOnce)
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
        var jsonNode = JObject.Parse(json);
        var idShort = (jsonNode.Property("IdShort").Value as JValue).Value<string>();
        using var scope = serviceProvider.CreateScope();
        var aasApiHelper = scope.ServiceProvider.GetService<AasApiHelperService>();
        var smRepoController = scope.ServiceProvider.GetService<SubmodelRepositoryAPIApiController>();
        var timeSeriesService = scope.ServiceProvider.GetService<TimeSeriesService>();

        foreach (var sm in Program.AllSubmodels())
        {
            var encodedSmId = ConvertHelper.ToBase64(sm.Id);

            foreach (var sme in sm.SubmodelElements)
            {
                if (sme is SubmodelElementCollection smc && smc.Category == AttributeTypeConstants.TYPE_RUNTIME)
                {
                    var triggerAttributeIdsJson = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.TriggerAttributeIds))?.Value;
                    var triggerAttributeIds = triggerAttributeIdsJson != null ? JsonConvert.DeserializeObject<IEnumerable<Guid>>(triggerAttributeIdsJson) : null;
                    if (triggerAttributeIds?.Contains(Guid.Parse(idShort)) != true)
                        continue;

                    var triggerAttributeIdStr = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.TriggerAttributeId))?.Value;
                    var triggerAttributeId = triggerAttributeIdStr != null ? Guid.Parse(triggerAttributeIdStr) : default(Guid?);
                    var dictionary = new Dictionary<string, object>();
                    foreach (var usedAttributeId in triggerAttributeIds)
                    {
                        var smeResult = smRepoController.GetSubmodelElementByPathSubmodelRepo(encodedSmId, idShortPath: usedAttributeId.ToString(), level: LevelEnum.Deep, extent: ExtentEnum.WithoutBlobValue) as ObjectResult;
                        var usedSme = smeResult.Value as ISubmodelElement;
                        var dto = aasApiHelper.ToAttributeDto(usedSme);
                        dictionary[dto.AttributeId.ToString()] = dto.Series.FirstOrDefault()?.v;
                    }

                    var expressionCompile = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.ExpressionCompile)).Value;
                    var runtimeValue = await CSharpScript.EvaluateAsync(expressionCompile, globals: new RuntimeExpressionGlobals { request = dictionary });

                    var series = TimeSeriesHelper.BuildSeriesDto(value: runtimeValue);
                    smc.UpdateSnapshot(series);
                    var attributeId = Guid.Parse(smc.IdShort);
                    await timeSeriesService.AddRuntimeSeries(attributeId, series);
                    _ = smRepoController.PutSubmodelElementByPathSubmodelRepo(sme, encodedSmId, sme.IdShort, level: LevelEnum.Deep);
                    await eventPublisher.Publish(AasEvents.SubmodelElementUpdated, sme);
                    await eventPublisher.Publish(AasEvents.AasUpdated, sm.Id);
                }
            }
        }

        Program.saveEnvDynamic(0);
    }
}