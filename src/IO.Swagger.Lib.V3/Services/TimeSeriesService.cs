namespace IO.Swagger.Lib.V3.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AasxServer;
using AasxServerStandardBib;
using AasxServerStandardBib.Models;
using AasxServerStandardBib.Services;
using AasxServerStandardBib.Utils;
using Extensions;
using IO.Swagger.Controllers;


public class TimeSeriesService(
    SubmodelRepositoryAPIApiController smRepoController,
    EventPublisher eventPublisher)
{
    private static readonly ConcurrentDictionary<Guid, List<TimeSeriesDto>> _runtimeSeries = new();
    private static readonly ConcurrentDictionary<(string DeviceId, string MetricKey), List<TimeSeriesDto>> _deviceMetricSeries = new();

    public async Task<IEnumerable<TimeSeriesDto>> GetRuntimeSeries(Guid attributeId)
    {
        var seriesList = _runtimeSeries.GetOrAdd(attributeId, _ => new());
        lock (seriesList)
        { return seriesList.OrderByDescending(s => s.ts); }
    }

    public async Task<IEnumerable<TimeSeriesDto>> GetDeviceMetricSeries(string deviceId, string metricKey)
    {
        var seriesList = _deviceMetricSeries.GetOrAdd((deviceId, metricKey), _ => new());
        lock (seriesList)
        { return seriesList.OrderByDescending(s => s.ts); }
    }

    public async Task<TimeSeriesDto> GetDeviceMetricSnapshot(string deviceId, string metricKey)
    {
        var seriesList = _deviceMetricSeries.GetOrAdd((deviceId, metricKey), _ => new());
        lock (seriesList)
        { return seriesList.OrderByDescending(s => s.ts).FirstOrDefault(); }
    }

    public async Task AddRuntimeSeries(Guid attributeId, TimeSeriesDto series)
    {
        var seriesList = _runtimeSeries.GetOrAdd(attributeId, _ => new());
        lock (seriesList)
        {
            seriesList.Add(series);
        }
    }

    public async Task AddDeviceMetricSeries(string deviceId, string metricKey, TimeSeriesDto series)
    {
        var seriesList = _deviceMetricSeries.GetOrAdd((deviceId, metricKey), _ => new());
        lock (seriesList)
        {
            seriesList.Add(series);
        }

        var (sm, dynamicAttr) = await GetDynamicAttributeSmc(deviceId, metricKey);
        if (dynamicAttr is not null)
        {
            dynamicAttr.UpdateSnapshot(series);
            var encodedSmId = ConvertHelper.ToBase64(sm.Id);
            _ = smRepoController.PutSubmodelElementByPathSubmodelRepo(dynamicAttr, encodedSmId, dynamicAttr.IdShort, level: Swagger.Models.LevelEnum.Deep);
            await eventPublisher.Publish(AasEvents.SubmodelElementUpdated, dynamicAttr);
            await eventPublisher.Publish(AasEvents.AasUpdated, sm.Id);
            Program.saveEnvDynamic(0);
        }
    }

    public async Task<(ISubmodel Submodel, ISubmodelElementCollection Smc)> GetDynamicAttributeSmc(string deviceId, string metricKey)
    {
        foreach (var sm in Program.AllSubmodels())
        {
            foreach (var sme in sm.SubmodelElements)
            {
                if (sme is not ISubmodelElementCollection smc || sme.Category != AttributeTypeConstants.TYPE_DYNAMIC)
                    continue;

                var aDeviceId = smc.FindFirstIdShortAs<IProperty>("DeviceId").Value;
                var aMetricKey = smc.FindFirstIdShortAs<IProperty>("MetricKey").Value;
                if (aDeviceId == deviceId && aMetricKey == metricKey)
                    return (sm, smc);
            }
        }
        return default;
    }
}