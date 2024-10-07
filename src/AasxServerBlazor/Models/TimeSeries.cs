using System;

namespace AasxServerBlazor.Models
{
    public class TimeSeries
    {
        public Guid AssetId { get; set; }
        public Guid AttributeId { get; set; }
        public double? Value { get; set; }
        public double? LastGoodValue { get; set; }
        public string ValueText { get; set; }
        public string LastGoodValueText { get; set; }
        public bool? ValueBoolean { get; set; }
        public bool? LastGoodValueBoolean { get; set; }
        public long UnixTimestamp { get; set; }
        public long LastGoodUnixTimestamp { get; set; }
        public DateTime DateTime { get; set; }
        public string DataType { get; set; }
        public string AliasAttributeType { get; set; }
        public int? SignalQualityCode { get; set; }
        public long RowNum { get; set; }

        public static bool? ParseTimeseriesBoolean(string valueText)
            => valueText == "1" ? true : valueText == "0" ? false : null;
    }
}
