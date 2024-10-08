using System;
using System.Linq;
using System.Dynamic;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AHI.Infrastructure.SharedKernel.Extension;
using AasxServerStandardBib.Models;

namespace AasxServerStandardBib.Utils
{
    public static class ObjectExtension
    {
        public static T ConvertToNumber<T>(this object input)
        {
            var validTypes = new[] { typeof(int), typeof(long), typeof(double), typeof(decimal), typeof(int?), typeof(long?), typeof(double?), typeof(decimal?) };
            if (!validTypes.Any(type => type == typeof(T)))
                throw new InvalidCastException();


            if (input == null || (string)input == string.Empty)
                return default;

            var type = typeof(T);
            var nullableType = Nullable.GetUnderlyingType(type);

            if (nullableType != null)
                return (T)Convert.ChangeType(input, nullableType);
            else
                return (T)Convert.ChangeType(input, type);
        }

        public static bool ConvertToBoolean(this object value)
        {
            if (value == null)
                return false;

            if (bool.TryParse(value.ToString(), out var result))
                return result;

            return false;
        }

        public static bool IsLessThanOrEqualsTo(this object value, int length)
        {
            if (value == null)
            {
                return true;
            }

            return value.ToString().Count() <= length;
        }

        public static bool ParseResultWithDataType(this object value, string dataType)
        {
            switch (dataType)
            {
                case DataTypeConstants.TYPE_BOOLEAN:
                    return value is bool;

                case DataTypeConstants.TYPE_TIMESTAMP:
                    return double.TryParse(value.ToString(), out _);

                case DataTypeConstants.TYPE_DOUBLE:
                    return double.TryParse(value.ToString(), out _);
                case DataTypeConstants.TYPE_INTEGER:
                    if (double.TryParse(value.ToString(), out var intValue))
                    {
                        try
                        {
                            Convert.ToInt32(intValue);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                    else
                        return false;
                // case "text":
                //     return value is string || value is char;
                case DataTypeConstants.TYPE_DATETIME:
                    return DateTime.TryParse(value.ToString(), out _);
                default:
                    return true;
            }
        }

        public static object ParseValueWithDataType(this object value, string dataType, string valueText, bool isRawData)
        {
            if (value == null)
            {
                if (valueText == null)
                {
                    return value;
                }
                value = valueText;
            }

            if (string.IsNullOrEmpty(valueText) && string.IsNullOrEmpty(value.ToString()))
                return null;

            switch (dataType)
            {
                case DataTypeConstants.TYPE_BOOLEAN:
                    return ParseValueToBoolean(value, isRawData, valueText);
                case DataTypeConstants.TYPE_DOUBLE:
                case DataTypeConstants.TYPE_INTEGER:
                    //https://dev.azure.com/AssetHealthInsights/Asset%20Backlogs/_workitems/edit/11775
                    return double.TryParse(value.ToString(), out var v) && !double.IsNaN(v) ? value as object : valueText;
                case DataTypeConstants.TYPE_TEXT:
                    return Regex.IsMatch(value.ToString(), "^(?=.{0,255}$)") ? value as object : valueText;
                case DataTypeConstants.TYPE_TIMESTAMP:
                    return double.TryParse(value.ToString(), out _) ? value as object : valueText;
                case DataTypeConstants.TYPE_DATETIME:
                    return DateTime.TryParse(value.ToString(), out _) ? value as object : valueText;
                default:
                    return valueText;
            }
        }

        private static object ParseValueToBoolean(object value, bool isRawData, string valueText)
        {
            if (isRawData) //https://dev.azure.com/AssetHealthInsights/Asset%20Backlogs/_workitems/edit/11775
            {
                if (bool.TryParse(value.ToString(), out var boolVal))
                {
                    return boolVal;
                }
                else if (value.ToString() == "1" || value.ToString() == "0")
                {
                    return value.ToString() == "1";
                }
                else
                    return valueText;
            }
            else
            {
                return double.TryParse(value.ToString(), out var vb) && !double.IsNaN(vb) ? value as object : valueText;
            }
        }

        public static dynamic ToExpandoObject(this object value)
        {
            var dapperRowProperties = value as IDictionary<string, object>;
            IDictionary<string, object> expando = new ExpandoObject();
            foreach (var property in dapperRowProperties)
            {
                var valueProperty = property.Value;
                if (valueProperty == null)
                {
                    expando.Add(property.Key, valueProperty);
                    continue;
                }
                if (valueProperty.GetType() == typeof(DateTime))
                {
                    var datetimeValue = Convert.ToDateTime(valueProperty).ToString(Constant.DefaultDateTimeFormat);
                    expando.Add(property.Key, datetimeValue);
                }
                else
                {
                    expando.Add(property.Key, valueProperty);
                }
            }
            return expando as ExpandoObject;
        }
    }
}
