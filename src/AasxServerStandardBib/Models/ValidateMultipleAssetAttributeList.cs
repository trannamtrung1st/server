using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AasxServerStandardBib.Models
{
    [ValidateNever]
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
