using System;
using System.Collections.Generic;

namespace AasxServerStandardBib.Models
{
    public class AttributeMapping : Dictionary<string, object>
    {
        public AttributeMapping() : base(StringComparer.InvariantCultureIgnoreCase)
        { }

        public Guid TemplateAttributeId
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.TEMPLATE_ATTRIBUTE_ID) && this[PayloadConstants.TEMPLATE_ATTRIBUTE_ID] != null)
                {
                    return Guid.Parse(this[PayloadConstants.TEMPLATE_ATTRIBUTE_ID] as string);
                }
                return Guid.Empty;
            }
        }

        public Guid? TriggerAttributeId
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.TRIGGER_ATTRIBUTE_ID) && this[PayloadConstants.TRIGGER_ATTRIBUTE_ID] != null)
                {
                    return Guid.Parse(this[PayloadConstants.TRIGGER_ATTRIBUTE_ID] as string);
                }
                return null;
            }
        }

        public string DeviceId
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.DEVICE_ID) && this[PayloadConstants.DEVICE_ID] != null)
                {
                    return this[PayloadConstants.DEVICE_ID].ToString();
                }
                return null;
            }
        }

        public string MetricKey
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.METRIC_KEY) && this[PayloadConstants.METRIC_KEY] != null)
                {
                    return this[PayloadConstants.METRIC_KEY].ToString();
                }
                return null;
            }
        }

        public string MetricName
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.METRIC_NAME) && this[PayloadConstants.METRIC_NAME] != null)
                {
                    return this[PayloadConstants.METRIC_NAME].ToString();
                }
                return null;
            }
            set => this[PayloadConstants.METRIC_NAME] = value;
        }

        public Guid? RowVersion
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.ROW_VERSION) && this[PayloadConstants.ROW_VERSION] != null)
                {
                    return Guid.Parse(this[PayloadConstants.ROW_VERSION].ToString());
                }
                return null;
            }
        }

        public bool EnabledExpression
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.ENABLED_EXPRESSION) && this[PayloadConstants.ENABLED_EXPRESSION] != null)
                {
                    return Convert.ToBoolean(this[PayloadConstants.ENABLED_EXPRESSION]);
                }
                return false;
            }
        }

        public string DataType
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.DATA_TYPE) && this[PayloadConstants.DATA_TYPE] != null)
                {
                    return this[PayloadConstants.DATA_TYPE] as string;
                }
                return string.Empty;
            }
        }

        public int? DecimalPlace
        {
            get
            {
                var decimalPlace = 0;
                if (this.ContainsKey(PayloadConstants.DECIMAL_PLACE) && int.TryParse(this[PayloadConstants.DECIMAL_PLACE] as string, out decimalPlace))
                {
                    return decimalPlace;
                }
                return null;
            }
        }

        public bool? ThousandSeparator
        {
            get
            {
                var thousandSeparator = false;
                if (this.ContainsKey(PayloadConstants.THOUSAND_SEPARATOR) && bool.TryParse(this[PayloadConstants.THOUSAND_SEPARATOR] as string, out thousandSeparator))
                {
                    return thousandSeparator;
                }
                return null;
            }
        }

        public object Value
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.VALUE) && this[PayloadConstants.VALUE] != null)
                {
                    return this[PayloadConstants.VALUE] as object;
                }
                return null;
            }
        }

        public string IntegrationId
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.INTEGRATION_ID) && this[PayloadConstants.INTEGRATION_ID] != null)
                {
                    return this[PayloadConstants.INTEGRATION_ID].ToString();
                }
                return null;
            }
        }

        public Guid? DeviceTemplateId
        {
            get
            {
                if (this.ContainsKey(PayloadConstants.DEVICE_TEMPLATE_ID) && this[PayloadConstants.DEVICE_TEMPLATE_ID] != null)
                {
                    return Guid.Parse(this[PayloadConstants.DEVICE_TEMPLATE_ID] as string);
                }
                return null;
            }
        }
    }
}
