using System;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AasxServerStandardBib.Models
{
    [ValidateNever]
    public class ValidatAttributeRequest
    {
        public string MetricKey { get; set; }

        public string CommandMetricKey { get; set; }

        public string AttributeType { get; set; }

        public Guid? DeviceTemplateId { get; set; }

        public Guid? ChannelId { get; set; }

        public string DeviceId { get; set; }

        public string CommandDeviceId { get; set; }

        public string DeviceIdIntegration { get; set; }

        public string MetricKeyIntegration { get; set; }

        public int UomId { get; set; }

        public Guid? AliasAssetId { get; set; }

        public Guid AttributeId { get; set; }

        public Guid? AliasAttributeId { get; set; }

        public Guid? TriggerAttributeId { get; set; }

        public string Expression { get; set; }

        public Guid Id { get; set; }

        public Guid AssetId { get; set; }

        public object Value { get; set; }

        public string DataType { get; set; }

        public bool EnabledExpression { get; set; }

        public bool IsTriggerVisibility { get; set; }

        public string Name { get; set; }

        public Guid? AssetTemplateId { get; set; }
    }
}
