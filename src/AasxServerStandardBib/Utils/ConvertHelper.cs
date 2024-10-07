namespace AasxServerStandardBib.Utils;

using System;
using System.Text;

public static class ConvertHelper
{
    public static string ToBase64(string source)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(source));
    }
}