using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AasxServerStandardBib.Models
{
    [ValidateNever]
    public class TimeSeriesDto
    {
        public long ts { get; set; }
        public object v { get; set; }
        public object l { get; set; }
        public long lts { get; set; }
        public int? q { get; set; }
    }
}
