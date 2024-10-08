namespace AasxServerStandardBib.Models
{
    public static class RegexConstants
    {
        public const string PATTERN_EXPRESSION_KEY = @"\$\{(.+?)\}\$";

        public const string PATTERN_PROJECT_ID = @"^[a-fA-F0-9-]{36}";
        public const string PATTERN_TELEMETRY_TOPIC = @"^[a-fA-F0-9-]{36}\/devices\/[^\/#+$*]+\/telemetry$";
        public const string PATTERN_COMMAND_TOPIC = @"^[a-fA-F0-9-]{36}\/devices\/[^\/#+$*]+\/commands";
    }
}
