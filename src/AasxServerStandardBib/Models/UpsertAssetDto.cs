using System.Collections.Generic;

namespace AasxServerStandardBib.Models
{
    public class UpsertAssetDto
    {
        public List<BaseJsonPathDocument> Data { set; get; } = new List<BaseJsonPathDocument>();
    }
}