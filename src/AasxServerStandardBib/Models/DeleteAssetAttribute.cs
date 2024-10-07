using System;

namespace AasxServerStandardBib.Models
{
    public class DeleteAssetAttribute
    {
        public bool ForceDelete { get; set; }
        public Guid[] Ids { get; set; }
    }
}
