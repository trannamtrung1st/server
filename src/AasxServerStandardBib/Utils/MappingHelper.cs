namespace AasxServerStandardBib.Utils;

using AasxServerStandardBib.Models;

public static class MappingHelper
{
    public static DataTypeDefXsd ToAasDataType(string dataType)
    {
        return dataType switch
        {
            DataTypeConstants.TYPE_TEXT => DataTypeDefXsd.String,
            DataTypeConstants.TYPE_BOOLEAN => DataTypeDefXsd.Boolean,
            DataTypeConstants.TYPE_DATETIME => DataTypeDefXsd.DateTime,
            DataTypeConstants.TYPE_DOUBLE => DataTypeDefXsd.Double,
            DataTypeConstants.TYPE_INTEGER => DataTypeDefXsd.Integer,
            DataTypeConstants.TYPE_TIMESTAMP => DataTypeDefXsd.Long,
            _ => DataTypeDefXsd.String,
        };
    }

    public static string ToAhiDataType(DataTypeDefXsd aasDataType)
    {
        return aasDataType switch
        {
            DataTypeDefXsd.String => DataTypeConstants.TYPE_TEXT,
            DataTypeDefXsd.Boolean => DataTypeConstants.TYPE_BOOLEAN,
            DataTypeDefXsd.DateTime => DataTypeConstants.TYPE_DATETIME,
            DataTypeDefXsd.Double => DataTypeConstants.TYPE_DOUBLE,
            DataTypeDefXsd.Integer => DataTypeConstants.TYPE_INTEGER,
            DataTypeDefXsd.Long => DataTypeConstants.TYPE_TIMESTAMP,
            _ => DataTypeConstants.TYPE_TEXT, // Default to TEXT for unsupported types
        };
    }
}