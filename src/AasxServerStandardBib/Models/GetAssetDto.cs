using System;
using System.Collections.Generic;

namespace AasxServerStandardBib.Models
{
    public abstract class BaseAssetDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string NormalizeName => Name;
        public int RetentionDays { get; set; }
        public Guid? ParentAssetId { get; set; }
        public Guid? AssetTemplateId { get; set; }
        public string AssetTemplateName { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string CurrentUserUpn { set; get; }
        public DateTime? CurrentTimestamp { set; get; }
        public string RequestLockUserUpn { set; get; }
        public DateTime? RequestLockTimestamp { set; get; }
        public DateTime? RequestLockTimeout { set; get; }
        public GetAssetSimpleDto Parent { get; set; }
        public IEnumerable<AssetAttributeDto> Attributes { get; set; } = new List<AssetAttributeDto>();
        public bool HasWarning { get; set; }
        public string ResourcePath { get; set; }
        public string CreatedBy { get; set; }
        public bool IsDocument { get; set; }
    }

    public class GetAssetDto : BaseAssetDto
    {
        public IEnumerable<GetAssetSimpleDto> Children { get; set; } = new List<GetAssetSimpleDto>();
    }

    public class GetFullAssetDto : BaseAssetDto
    {
        public ICollection<GetFullChildrenAssetDto> Children { get; set; }
    }

    public class GetFullChildrenAssetDto : GetFullAssetDto
    {
    }
}
