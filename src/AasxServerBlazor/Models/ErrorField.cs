using System.Collections.Generic;

namespace AasxServerBlazor.Models
{
    public class ErrorField
    {
        public string Name { get; set; }
        public string ErrorCode { get; set; }
        public IDictionary<string, object> Payload { get; set; }

        public ErrorField(string name, string errorCode, IDictionary<string, object> payload = null)
        {
            Name = name;
            ErrorCode = errorCode;
            Payload = payload;
        }
    }
}
