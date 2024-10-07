using System.Collections.Generic;

namespace AasxServerStandardBib.Models
{
    public class ValidateMultipleAssetAttributeList
    {
        public ValidationType ValidationType { get; set; } = ValidationType.Asset;

        public ValidationAction ValidationAction { get; set; } = ValidationAction.Upsert;

        public int StartIndex { get; set; }

        public int BatchSize { get; set; }

        public IEnumerable<ValidatAttributeRequest> Attributes { get; set; }

        public ValidateMultipleAssetAttributeList()
        {
        }
    }
}
