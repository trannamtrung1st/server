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
using IO.Swagger.Lib.V3.Controllers;
using IO.Swagger.Controllers;
using AasxServerStandardBib.Utils;
using IO.Swagger.Models;
using Microsoft.AspNetCore.Mvc;
using Extensions;
using System.Globalization;
using Newtonsoft.Json.Linq;
using AdminShellNS.Extensions;
using Microsoft.Extensions.Logging;

public class CalculateRuntimeAttributeHandler(
    MqttClientManager mqttClientManager,
    IServiceProvider serviceProvider,
    EventPublisher eventPublisher,
    ILogger<CalculateRuntimeAttributeHandler> logger) : IEventHandler
{
    public async Task Start()
    {
        var subscriber = await mqttClientManager.GetSubscriber();

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
        var ahiAssetsController = scope.ServiceProvider.GetService<AhiAssetsController>();
        var smRepoController = scope.ServiceProvider.GetService<SubmodelRepositoryAPIApiController>();
        var allSms = Program.AllSubmodels();
        var triggeredRuntimeAttrs = new List<(ISubmodel Submodel, ISubmodelElement Sme)>();

        foreach (var sm in allSms)
        {
            var encodedSmId = ConvertHelper.ToBase64(sm.Id);

            foreach (var sme in sm.SubmodelElements)
            {
                if (sme is SubmodelElementCollection smc && smc.Category == AttributeTypeConstants.TYPE_RUNTIME)
                {
                    var triggerAttributeIdsJson = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.TriggerAttributeIds))?.Value;
                    var triggerAttributeIds = triggerAttributeIdsJson != null ? JsonConvert.DeserializeObject<IEnumerable<Guid>>(triggerAttributeIdsJson) : null;
                    if (!triggerAttributeIds?.Contains(Guid.Parse(idShort)) == true)
                        continue;

                    var triggerAttributeIdStr = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.TriggerAttributeId))?.Value;
                    var triggerAttributeId = triggerAttributeIdStr != null ? Guid.Parse(triggerAttributeIdStr) : default(Guid?);
                    var dictionary = new Dictionary<string, object>();
                    foreach (var usedAttributeId in triggerAttributeIds)
                    {
                        if (usedAttributeId == triggerAttributeId)
                            continue;

                        var smeResult = smRepoController.GetSubmodelElementByPathSubmodelRepo(encodedSmId, idShortPath: usedAttributeId.ToString(), level: LevelEnum.Deep, extent: ExtentEnum.WithoutBlobValue) as ObjectResult;
                        var usedSme = smeResult.Value as ISubmodelElement;
                        var dto = ahiAssetsController.ToAttributeDto(usedSme);
                        dictionary[dto.AttributeId.ToString()] = dto.Series.FirstOrDefault().v;
                    }

                    var expressionCompile = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.ExpressionCompile)).Value;
                    var runtimeValue = await CSharpScript.EvaluateAsync(expressionCompile, globals: new RuntimeExpressionGlobals { request = dictionary });

                    var dataType = smc.FindFirstIdShortAs<IProperty>("DataType").Value;
                    var snapshotSmc = smc.FindFirstIdShortAs<ISubmodelElementCollection>("Snapshot");
                    if (snapshotSmc.Value.IsNullOrEmpty())
                    {
                        snapshotSmc.Value = [];
                        snapshotSmc.Add(new Property(valueType: MappingHelper.ToAasDataType(dataType))
                        {
                            IdShort = "Value",
                            DisplayName = [new LangStringNameType("en-US", "Value")]
                        });
                        snapshotSmc.Add(new Property(valueType: DataTypeDefXsd.Long)
                        {
                            IdShort = "Timestamp",
                            DisplayName = [new LangStringNameType("en-US", "Timestamp")]
                        });
                    }
                    var snapshot = snapshotSmc.FindFirstIdShortAs<IProperty>("Value");
                    snapshot.Value = $"{runtimeValue}";
                    var timestamp = snapshotSmc.FindFirstIdShortAs<IProperty>("Timestamp");
                    timestamp.Value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

                    var series = smc.FindFirstIdShortAs<ISubmodelElementList>("SeriesData");
                    var singleSeries = Copying.Deep(snapshotSmc);
                    singleSeries.IdShort = Guid.NewGuid().ToString();
                    singleSeries.DisplayName = [new LangStringNameType("en-US", "Series")];
                    series.Value ??= [];
                    series.Value.Add(singleSeries);

                    triggeredRuntimeAttrs.Add((sm, smc));
                }
            }
        }

        foreach (var (sm, sme) in triggeredRuntimeAttrs)
        {
            var encodedSmId = ConvertHelper.ToBase64(sm.Id);
            _ = smRepoController.PutSubmodelElementByPathSubmodelRepo(sme, encodedSmId, sme.IdShort, level: LevelEnum.Deep);
            await eventPublisher.Publish(AasEvents.SubmodelElementUpdated, sme);
        }

        Program.saveEnvDynamic(0);
    }
}