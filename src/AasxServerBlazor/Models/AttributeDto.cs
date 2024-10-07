using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace AasxServerBlazor.Models
{
    public class AttributeDto
    {
        public Guid AttributeId { get; set; }
        public List<TimeSeriesDto> Series { get; set; }
        public string AttributeName { get; set; }
        public string AttributeNameNormalize { get; set; }
        public GetSimpleUomDto Uom { get; set; }
        public int? DecimalPlace { get; set; }
        public bool? ThousandSeparator { get; set; }
        public string GapfillFunction { get; set; }
        public string AttributeType { get; set; }
        public string DataType { get; set; }
        public int? QualityCode { get; set; }
        public string Quality { get; set; }
        public string AliasAttributeType { get; set; }
    }
}