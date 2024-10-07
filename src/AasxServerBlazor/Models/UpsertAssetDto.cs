using System.Collections.Generic;

namespace AasxServerBlazor.Models
{
    public class UpsertAssetDto
    {
        public List<BaseJsonPathDocument> Data { set; get; } = new List<BaseJsonPathDocument>();
    }
}