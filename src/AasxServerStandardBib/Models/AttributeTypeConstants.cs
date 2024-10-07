
namespace AasxServerStandardBib.Models
{

    public static class AttributeTypeConstants
    {
        public const string TYPE_STATIC = "static";
        public const string TYPE_DYNAMIC = "dynamic";
        public const string TYPE_RUNTIME = "runtime";
        public const string TYPE_ALIAS = "alias";
        public const string TYPE_INTEGRATION = "integration";
        public const string TYPE_COMMAND = "command";
        public static readonly string[] TIME_SERIES_ATTRIBUTES = new[] { TYPE_DYNAMIC, TYPE_RUNTIME };
        public static readonly string[] ALLOWED_ATTRIBUTE_TYPES = new string[] { TYPE_STATIC, TYPE_DYNAMIC, TYPE_RUNTIME, TYPE_ALIAS, TYPE_INTEGRATION, TYPE_COMMAND };
        public static bool IsValidTemplateTypes(string type)
        {
            switch (type)
            {
                case TYPE_STATIC:
                case TYPE_DYNAMIC:
                case TYPE_RUNTIME:
                case TYPE_INTEGRATION:
                case TYPE_ALIAS:
                case TYPE_COMMAND:
                    return true;
                default:
                    return false;
            }
        }
    }
}
