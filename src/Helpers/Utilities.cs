
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MayShows.Helpers;

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
        var rgx = new Regex(@"\d{4}-\d{2}-\d{2}");
        var mat = rgx.Match(str);
        if (mat.Success)
        {
            var dtStr = mat.ToString();
            string[] formats = ["yyyy-MM-dd"];
            DateTime parsedDateTime;
            var didWork = DateTime.TryParseExact(dtStr, formats, CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out parsedDateTime);
            return didWork ? DateOnly.FromDateTime(parsedDateTime) : null;
        }
        return null;
    }
}