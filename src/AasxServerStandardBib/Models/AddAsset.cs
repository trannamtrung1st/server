using System;
using System.Collections.Generic;

namespace AasxServerStandardBib.Models
{
    public class AddAsset
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public Guid? ParentAssetId { get; set; }
        public Guid? AssetTemplateId { get; set; }
        public int? RetentionDays { get; set; }
        public bool IsDocument { get; set; }
        public IEnumerable<AssetAttribute> Attributes { get; set; } = new List<AssetAttribute>();
        public IEnumerable<AttributeMapping> Mappings { get; set; } = new List<AttributeMapping>();
        public IEnumerable<AddAsset> Children { get; set; } = new List<AddAsset>();
    }
}
