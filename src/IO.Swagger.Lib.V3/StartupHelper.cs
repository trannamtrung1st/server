namespace IO.Swagger.Lib.V3;

using AasxServerStandardBib.EventHandlers.Abstracts;
using AasxServerStandardBib.Services;
using IO.Swagger.Controllers;
using IO.Swagger.Lib.V3.Controllers;
using IO.Swagger.Lib.V3.EventHandlers;
using IO.Swagger.Lib.V3.Services;
using Microsoft.Extensions.DependencyInjection;

public static class StartupHelper
{
    public static void AddAhiServices(IServiceCollection services)
    {
        services
            .AddScoped<RuntimeAssetAttributeHandler>()
            .AddSingleton<MqttClientManager>()
            .AddScoped<TimeSeriesService>()
            .AddScoped<AasApiHelperService>()
            .AddSingleton<NotificationService>()

            .AddSingleton<EventPublisher>()
            .AddSingleton<IEventHandler, CalculateRuntimeAttributeHandler>()
            .AddSingleton<IEventHandler, NotifyAssetChangedHandler>()

            .AddScoped<AhiAssetsController>()
            .AddScoped<AssetAdministrationShellRepositoryAPIApiController>()
            .AddScoped<SubmodelRepositoryAPIApiController>();
    }
}