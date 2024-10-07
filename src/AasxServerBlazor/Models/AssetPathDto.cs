using System;
using System.Collections.Generic;

namespace AasxServerBlazor.Models
{
    public class AssetPathDto
    {
        // 3
        public Guid AssetId { get; set; }

        // 1,2,3
        public string PathId { get; set; }

        // a1,a2,a3
        public string PathName { get; set; }

        public IEnumerable<AssetAttributeDto> Attributes { get; set; } = new List<AssetAttributeDto>();

        public AssetPathDto(Guid assetId, string pathId, string pathName)
        {
            AssetId = assetId;
            PathId = pathId;
            PathName = pathName;
        }
    }
}
