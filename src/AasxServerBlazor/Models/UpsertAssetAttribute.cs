
using System;
using Microsoft.AspNetCore.JsonPatch;

namespace AasxServerBlazor.Models
{
    public class UpsertAssetAttribute
    {
        public JsonPatchDocument Data { set; get; }

        public Guid AssetId { set; get; }
    }
}
