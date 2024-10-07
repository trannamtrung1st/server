using System.Text.RegularExpressions;

namespace AasxServerStandardBib.Models
{
    public static class PostgresFunction
    {
        public const string TIME_BUCKET = "time_bucket";
        public const string TIME_BUCKET_GAPFILL = "time_bucket_gapfill";
        public const string DEFAULT_FUNCTION = "default_function";

        public static readonly string[] ALLOW_GAPFILL_FUNCTION = new string[] { TIME_BUCKET, TIME_BUCKET_GAPFILL };



        public static readonly string[] ALLOW_AGGREGATE_FUNCTION = new string[] { "min", "max", "count", "sum", "avg", };

        public static readonly string[] ALLOW_GRANULARITY = new string[] { "minute", "minutes", "hour", "hours", "day", "days", "week", "weeks", "month", "months", "year", "years" };

        public static bool IsValidGranularity(string timegrain)
        {
            if (string.IsNullOrEmpty(timegrain))
            {
                return true;
            }
            var regexPattern = @"^([0-9]{1,3})\s(minutes|minute|hours|hour|days|day|weeks|week|months|month|year|years)$";
            return Regex.IsMatch(timegrain.ToLowerInvariant(), regexPattern);
        }
    }
}
