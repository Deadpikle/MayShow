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
using Avalonia.Platform.Storage;
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
        #if IOS
        var path = Path.Combine(
            MobileUtilities.GetDataDirBasePath(),
            "MayShow"
        );
        #else
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MayShow"
        );
        #endif
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }
    
    public static string GetTmpDataPath()
    {
        var path = Path.Combine(
            GetInternalDataPath(),
            "Tmp"
        );
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    public static string GetPDFBackupDataPath()
    {
        var path = Path.Combine(
            GetInternalDataPath(),
            "PDF Backups"
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

    public static void SaveReportDataSync(PDFReport reportData, string path, JsonTypeInfo<PDFReport>? context = null)
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
        reportData.SaveDataFileInfo();
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
        await reportData.SaveDataFileInfoAsync();
    }

    public static FilePickerFileType[] GetReportFilePickerFileTypes()
    {
        return [
            new FilePickerFileType("All Types")
            {
                Patterns = Constants.AllowedFileExtensionPatterns,
                AppleUniformTypeIdentifiers = Constants.FilePickerAppleTypeIdentifiers,
                MimeTypes = Constants.FilePickerMimeTypes,
            },
            FilePickerFileTypes.ImageAll, 
            new FilePickerFileType("HEIC Images")
            {
                Patterns = [ "*.heic" ],
                AppleUniformTypeIdentifiers = [ "public.heic" ],
                MimeTypes = [ "image/heic" ]
            },
            FilePickerFileTypes.Pdf,
        ];
    }

    public static PDFReport? ImportReportDataJson(string path, string? uuid = null, string? baseFolder = null)
    {
        var internalPath = Utilities.GetInternalDataPath();
        uuid ??= Guid.NewGuid().ToString();
        var json = File.ReadAllText(path);
        var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        var report = File.Exists(path) ? JsonSerializer.Deserialize(json, jsonContext.PDFReport) : null;
        if (report == null)
        {
            return null;
        }
        baseFolder ??= report?.BaseFolder ?? "";
        var reportInfo = new PDFReport()
        {
            Title = report?.Title ?? "",
            UUID = uuid,
            LastSaved = report?.LastSaved,
            #if !IOS
            BaseFolder = baseFolder,
            #endif
            LastGenerated = report?.LastGenerated,
            LastGeneratedBackupPath = report?.LastGeneratedBackupPath,
            Files = report?.Files ?? []
        };
        // sync UUIDs
        // if UUID exists in BaseFolder/(Constants.ReportSavedDataFileName), use that UUID instead.
        var externalReportDataPath = Path.Combine(reportInfo.BaseFolder, Constants.ReportSavedDataFileName);
        if (File.Exists(externalReportDataPath))
        {
            var originalReportData = JsonSerializer.Deserialize(File.ReadAllText(externalReportDataPath), jsonContext.PDFReport);
            if (originalReportData != null)
            {
                if (!string.IsNullOrWhiteSpace(originalReportData.UUID))
                {
                    if (Directory.Exists(Path.Combine(internalPath, uuid)))
                    {
                        Directory.Move(Path.Combine(internalPath, uuid), Path.Combine(internalPath, originalReportData.UUID));
                        reportInfo.UUID = originalReportData.UUID;
                    }
                }
                else
                {
                    // update UUID so they are in sync between internal and external folders
                    originalReportData.UUID = reportInfo.UUID;
                    Utilities.SaveReportDataSync(originalReportData, externalReportDataPath, jsonContext.PDFReport);
                }
            }
        }
        // update report data itself and move to internal -- everything is moving to internal storage dir, 
        // so if there is external data, use whatever is the most recent.
        // reportInfo.UUID now has the UUID we want to use.
        var internalReportFolderPath = Path.Combine(internalPath, reportInfo.UUID);
        var internalDataFilePath = Path.Combine(internalReportFolderPath, Constants.ReportSavedDataFileName);
        if (!Path.Exists(internalReportFolderPath))
        {
            // internal path doesn't exist at all so never saved internally before. 
            // make the dir and copy data to internal dir.
            Directory.CreateDirectory(internalReportFolderPath);
            if (File.Exists(externalReportDataPath))
            {
                File.Copy(externalReportDataPath, Path.Combine(internalReportFolderPath, Constants.ReportSavedDataFileName));
            }
        }
        else
        {
            // see which JSON file is newer (based on last saved time) and use that data.
            if (!File.Exists(internalDataFilePath))
            {
                // internal file doesn't exist, copy in from external
                if (File.Exists(externalReportDataPath))
                {
                    File.Copy(externalReportDataPath, internalDataFilePath);
                }
            }
            else if (File.Exists(internalDataFilePath) && File.Exists(externalReportDataPath))
            {
                // both files exist. load report data and compare dates.
                var internalReportData = JsonSerializer.Deserialize(File.ReadAllText(internalDataFilePath), jsonContext.PDFReport);
                var externalReportData = JsonSerializer.Deserialize(File.ReadAllText(externalReportDataPath), jsonContext.PDFReport);
                if (internalReportData != null && externalReportData != null)
                {
                    var isExternalNewer = (externalReportData.LastSaved ?? DateTime.MinValue) 
                        > (internalReportData.LastSaved ?? DateTime.MinValue);
                    if (isExternalNewer) // else internal is newer so nothing to do
                    {
                        File.Move(internalDataFilePath, Path.Combine(internalReportFolderPath, "old_report_data.json"));
                        File.Copy(externalReportDataPath, internalDataFilePath, true);
                        reportInfo.Title = externalReportData.Title;
                        reportInfo.LastSaved = externalReportData.LastSaved;
                    }
                }
                else if (internalReportData == null && externalReportData != null)
                {
                    // move data to internal dir
                    if (File.Exists(externalReportDataPath))
                    {
                        File.Copy(externalReportDataPath, internalDataFilePath, true);
                    }
                }
            }
        }
        #if !IOS
        reportInfo.BaseFolder = internalReportFolderPath;
        #endif
        // make sure BaseFolder is set right just in case -- now always points to internal directory.
        // (it's actually now redundant because all settings are internal...
        // but for now we'll just let it stick around.)
        if (File.Exists(internalDataFilePath))
        {
            var internalReportData = JsonSerializer.Deserialize(File.ReadAllText(internalDataFilePath), jsonContext.PDFReport);
            if (internalReportData != null)
            {
                #if !IOS
                internalReportData.BaseFolder = internalReportFolderPath;
                #endif
                Utilities.SaveReportDataSync(internalReportData, internalDataFilePath, jsonContext.PDFReport);
            }
        }
        return reportInfo;
    }
}