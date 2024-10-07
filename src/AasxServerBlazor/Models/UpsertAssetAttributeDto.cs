using System.Collections.Generic;

namespace AasxServerBlazor.Models
{
    public class UpsertAssetAttributeDto
    {
        public IList<BaseJsonPathDocument> Data { set; get; }
        public UpsertAssetAttributeDto()
        {
            Data = new List<BaseJsonPathDocument>();
        }
    }
}
