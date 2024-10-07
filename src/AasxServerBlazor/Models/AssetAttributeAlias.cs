using System;

namespace AasxServerBlazor.Models
{
    public class AssetAttributeAlias
    {
        public Guid Id { get; set; }
        public Guid AssetAttributeId { get; set; }
        public Guid? AliasAssetId { get; set; }
        public Guid? AliasAttributeId { get; set; }
        // public Guid? AliasAttributeTemplateId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        // public Guid? ParentId { get; set; }
        //public virtual AssetAttributeAlias Parent { get; set; }
        //public virtual IEnumerable<AssetAttributeAlias> Children { get; set; }
        public virtual AssetAttribute AssetAttribute { get; set; }
        // public virtual Asset AliasAsset { get; set; }
        // public virtual AssetAttribute AliasAssetAttribute { get; set; }
        //public virtual AssetAttributeTemplate AliasAssetAttributeTemplate { get; set; }

        // these attributes bellow should be load in runtime
        public string AliasAssetName { get; set; }
        public string AliasAttributeName { get; set; }
        public AssetAttributeAlias()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = DateTime.UtcNow;
            //Children = new List<AssetAttributeAlias>();
        }
    }
}
