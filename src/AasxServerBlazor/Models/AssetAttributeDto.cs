using System;
using Device.Application.Asset.Command.Model;

namespace AasxServerBlazor.Models
{
    public class AssetAttributeDto : IAssetAttribute
    {
        public Guid Id { get; set; }
        public Guid AssetId { get; set; }
        public string Name { get; set; }
        public object Value { get; set; }
        public string AttributeType { get; set; }
        public string DataType { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public bool Deleted { set; get; }
        public int? UomId { get; set; }
        public string NormalizeName => Name;
        public GetSimpleUomDto Uom { get; set; }
        public AttributeMapping Payload { get; set; }
        public int? DecimalPlace { get; set; }
        public bool? ThousandSeparator { get; set; }
        public int SequentialNumber { get; set; }

        // private static AttributeMapping GetAssetAttributePayload(Domain.Entity.AssetAttribute entity)
        // {
        //     if (entity.AssetAttributeDynamic != null)
        //     {
        //         return JObject.FromObject(
        //             new
        //             {
        //                 Id = entity.AssetAttributeDynamic.Id,
        //                 MetricKey = entity.AssetAttributeDynamic.MetricKey,
        //                 DeviceId = entity.AssetAttributeDynamic.DeviceId,
        //             })
        //             .ToObject<AttributeMapping>();
        //     }
        //     if (entity.AssetAttributeIntegration != null)
        //     {
        //         return JObject.FromObject(new
        //         {
        //             Id = entity.AssetAttributeIntegration.Id,
        //             IntegrationId = entity.AssetAttributeIntegration.IntegrationId,
        //             DeviceId = entity.AssetAttributeIntegration.DeviceId,
        //             MetricKey = entity.AssetAttributeIntegration.MetricKey
        //         }).ToObject<AttributeMapping>();
        //     }

        //     if (entity.AssetAttributeAlias != null)
        //     {
        //         return JObject.FromObject(new
        //         {
        //             Id = entity.AssetAttributeAlias.Id,
        //             AliasAssetId = entity.AssetAttributeAlias.AliasAssetId,
        //             AliasAttributeId = entity.AssetAttributeAlias.AliasAttributeId,
        //             AliasAssetName = entity.AssetAttributeAlias.AliasAssetName,
        //             AliasAttributeName = entity.AssetAttributeAlias.AliasAttributeName,
        //         }).ToObject<AttributeMapping>();
        //     }

        //     if (entity.AssetAttributeRuntime != null)
        //     {
        //         Guid? triggerAssetId = null;
        //         Guid? triggerAttribteId = null;
        //         bool? hasTriggerError = null;
        //         if (entity.AssetAttributeRuntime.IsTriggerVisibility)
        //         {
        //             var triggerAssetAttribute = entity.AssetAttributeRuntime.Triggers.Where(x => x.IsSelected).SingleOrDefault();
        //             if (triggerAssetAttribute == null)
        //             {
        //                 hasTriggerError = true;
        //             }
        //             else
        //             {
        //                 // single trigger
        //                 triggerAssetId = triggerAssetAttribute.TriggerAssetId;
        //                 triggerAttribteId = triggerAssetAttribute.TriggerAttributeId;
        //             }
        //         }
        //         return JObject.FromObject(new
        //         {
        //             Id = entity.AssetAttributeRuntime.Id,
        //             entity.AssetAttributeRuntime.EnabledExpression,
        //             entity.AssetAttributeRuntime.Expression,
        //             entity.AssetAttributeRuntime.ExpressionCompile,
        //             TriggerAssetId = triggerAssetId,
        //             TriggerAttributeId = triggerAttribteId,
        //             hasTriggerError = hasTriggerError
        //         }).ToObject<AttributeMapping>();
        //     }

        //     if (entity.AssetAttributeCommand != null)
        //     {
        //         return JObject.FromObject(new
        //         {
        //             Id = entity.AssetAttributeCommand.Id,
        //             entity.AssetAttributeCommand.DeviceId,
        //             entity.AssetAttributeCommand.MetricKey,
        //             entity.AssetAttributeCommand.Value,
        //             entity.AssetAttributeCommand.RowVersion,
        //             //az https://dev.azure.com/AssetHealthInsights/Asset%20Backlogs/_workitems/edit/75526
        //             DataType = entity.AssetAttributeCommand.Device?.Template?.Bindings?.First(x => x.Key == entity.AssetAttributeCommand.MetricKey).DataType
        //         }).ToObject<AttributeMapping>();
        //     }
        //     return null;
        // }
    }
}