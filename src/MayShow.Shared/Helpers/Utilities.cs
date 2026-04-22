#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MayShow.Models;

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

    public static string GetTempConvertedImagesFolderPath()
    {
        // get converted files directory path and create it if necessary
        var convertedDir = Path.Combine(GetInternalDataPath(), "converted");
        if (!Directory.Exists(convertedDir))
        {
            Directory.CreateDirectory(convertedDir);
        }
        return convertedDir;        
    }

    public static Guid GetUniqueReportGuid(Settings settings)
    {
        // Guid should be, well, unique already, BUT we are not going to take ANY chances.
        var internalPath = GetInternalDataPath();
        Guid guid = Guid.NewGuid();
        var isUnique = false;
        while (!isUnique)
        {
            var strUUID = guid.ToString();
            var didFind = false;
            foreach (var existingReport in settings.AllReportInfo)
            {
                if (existingReport.UUID == strUUID)
                {
                    didFind = true;
                    break;
                }
            }
            if (Directory.Exists(Path.Combine(internalPath, strUUID)))
            {
                didFind = true;
            }
            if (!didFind)
            {
                isUnique = true;
            }
            else
            {
                guid = Guid.NewGuid();
            }
        }
        return guid;
    }

    public static void SaveReportDataSync(PDFReport reportData, string path, JsonTypeInfo<PDFReport>? context)
    {
        if (context == null)
        {
            var jsonContext = new SourceGenerationContext(GetSerializerOptions());
            context = jsonContext.PDFReport;
        }
        using var memoryStream = new MemoryStream();
        JsonSerializer.Serialize(memoryStream, reportData, context);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        var updatedJson = reader.ReadToEnd();
        File.WriteAllText(path, updatedJson);
    }

    public static async Task SaveReportDataAsync(PDFReport reportData, string path, JsonTypeInfo<PDFReport>? context = null)
    {
        if (context == null)
        {
            var jsonContext = new SourceGenerationContext(GetSerializerOptions());
            context = jsonContext.PDFReport;
        }
        using var memoryStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryStream, reportData, context);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        var json = await reader.ReadToEndAsync();
        await File.WriteAllTextAsync(path, json);
    }
}