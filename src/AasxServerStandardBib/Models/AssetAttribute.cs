using System;

namespace AasxServerStandardBib.Models
{
    public class AssetAttribute
    {
        public Guid Id { get; set; }
        public Guid AssetId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        // public bool EnableExpression { get; set; } = true;
        public string Expression { get; set; }
        public string AttributeType { get; set; }
        public string DataType { get; set; }
        public int? UomId { get; set; }
        public int? DecimalPlace { get; set; }
        public bool? ThousandSeparator { get; set; }
        public int SequentialNumber { get; set; } = 1;
        public AttributeMapping Payload { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool IsStandalone { get; set; } = false;
        public AssetAttribute()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
        }

    }
}
