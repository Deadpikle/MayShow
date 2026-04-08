
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tmds.DBus.Protocol;

namespace MayShow.Helpers;

class Utilities
{
    public static JsonSerializerOptions GetSerializerOptions()
    {
        var opts = new JsonSerializerOptions 
        { 
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        return opts;
    }

    public static DateOnly? CheckValidDateInString(string str)
    {
        // https://stackoverflow.com/a/14918404/3938401
        // formats = regex format -> DateTime parsing format
        var formats = new Dictionary<string, string> 
        {
            {@"\d{4}-\d{2}-\d{2}", "yyyy-MM-dd"},
            {@"\d{4}.d{2}.d{2}", "yyyy.MM.dd"},
            {@"\d{8}", "yyyyMMdd"}
        };
        foreach (var data in formats)
        {
            var rgx = new Regex(data.Key);
            var mat = rgx.Match(str);
            if (mat.Success)
            {
                var dtStr = mat.ToString();
                var didWork = DateTime.TryParseExact(dtStr, [data.Value], CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out var parsedDateTime);
                if (didWork)
                {
                    return DateOnly.FromDateTime(parsedDateTime);
                }
            }
        }
        return null;
    }

    public static string GetInternalDataPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MayShow"
        );
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }
}