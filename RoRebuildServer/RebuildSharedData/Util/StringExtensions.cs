using System;
using System.Collections.Generic;
using System.Text;

namespace RebuildSharedData.Util;

public static class StringExtensions
{
    public static string Unescape(this string str)
    {
        if (str.StartsWith("\""))
            str = str.Substring(1, str.Length - 2);

        str = str.Replace("\\\"", "\"");

        return str;
    }
}