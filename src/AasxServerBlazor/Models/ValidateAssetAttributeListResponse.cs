using System;
using System.Collections.Generic;
using System.Linq;

namespace AasxServerBlazor.Models
{
    public class ValidateAssetAttributeListResponse
    {
        public bool IsSuccess => !Properties.Any();

        public IEnumerable<ErrorField> Properties { get; set; }

        public ValidateAssetAttributeListResponse()
        {
            Properties = new List<ErrorField>();
        }
    }
}
