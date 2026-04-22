using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MayShow.Helpers;

namespace MayShow.Models;

class Settings : ChangeNotifier
{
    private string _lastUsedPath;
    private bool _useDocnetPDFImageRendering;
    private bool _saveOutputPdfInWorkingDir;
    private string _outputPdfDir;
    private decimal _imageResizeThreshold;
    private Dictionary<string, string> _workingFolderToInternalFolderName; // obsolete
    private List<PDFReportInfo> _allReportInfo;
    public string _dataGridDateFormat;
    public string _reportDateFormat;
    public int _settingsVersion;

    public Settings() : base()
    {
        _lastUsedPath = "";
        _useDocnetPDFImageRendering = true;
        _saveOutputPdfInWorkingDir = true;
        _outputPdfDir = "";
        _imageResizeThreshold = 1.5m;
        _workingFolderToInternalFolderName = [];
        _allReportInfo = [];
        _settingsVersion = 2;
        _dataGridDateFormat = "dd/MM/yyyy";
        _reportDateFormat = "yyyy-MM-dd";
    }

    public Settings(Settings other)
    {
        _lastUsedPath = other.LastUsedPath;
        _useDocnetPDFImageRendering = other.UseDocnetPDFImageRendering;
        _saveOutputPdfInWorkingDir = other.SaveOutputPdfInWorkingDir;
        _outputPdfDir = other.OutputPdfDir;
        _imageResizeThreshold = other.ImageResizeThreshold;
        _workingFolderToInternalFolderName = other.WorkingFolderToInternalFolderName;
        _settingsVersion = other.SettingsVersion;
        _allReportInfo = other.AllReportInfo;
        _dataGridDateFormat = other.DataGridDateFormat;
        _reportDateFormat = other.ReportDateFormat;
    }

    [JsonInclude]
    public string LastUsedPath
    {
        get => _lastUsedPath;
        set { _lastUsedPath = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    [JsonPropertyName("UseDocnetPFDImageRendering")] // ...this typo now has to live because people have this saved on disk...
    public bool UseDocnetPDFImageRendering
    {
        get => _useDocnetPDFImageRendering;
        set { _useDocnetPDFImageRendering = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    public bool SaveOutputPdfInWorkingDir
    {
        get => _saveOutputPdfInWorkingDir;
        set { _saveOutputPdfInWorkingDir = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    public string OutputPdfDir
    {
        get => _outputPdfDir;
        set { _outputPdfDir = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    public decimal ImageResizeThreshold
    {
        get => _imageResizeThreshold;
        set { _imageResizeThreshold = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    public Dictionary<string, string> WorkingFolderToInternalFolderName
    {
        get => _workingFolderToInternalFolderName;
        set { _workingFolderToInternalFolderName = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    public List<PDFReportInfo> AllReportInfo
    {
        get => _allReportInfo;
        set { _allReportInfo = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    public int SettingsVersion
    {
        get => _settingsVersion;
        set { _settingsVersion = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    public string DataGridDateFormat
    {
        get => _dataGridDateFormat;
        set { _dataGridDateFormat = value; NotifyPropertyChanged(); }
    }

    [JsonInclude]
    public string ReportDateFormat
    {
        get => _reportDateFormat;
        set { _reportDateFormat = value; NotifyPropertyChanged(); }
    }

    public static string SettingsFileName = "settings.json";

    public static string GetSettingsPath()
    {
        var path = Utilities.GetInternalDataPath();
        return Path.Combine(path, SettingsFileName);
    }


    public string SaveSettingsNotAsync()
    {
        var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        using MemoryStream memoryStream = new MemoryStream();
        JsonSerializer.Serialize(memoryStream, this, jsonContext.Settings);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        var json = reader.ReadToEnd();
        File.WriteAllText(GetSettingsPath(), json);
        return json;
    }

    public async Task<string> SaveSettingsAsync()
    {
        var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        using MemoryStream memoryStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryStream, this, jsonContext.Settings);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        var json = await reader.ReadToEndAsync();
        await File.WriteAllTextAsync(GetSettingsPath(), json);
        return json;
    }

    private static Settings UpgradeSettings(Settings settings)
    {
        if (settings.SettingsVersion == 1)
        {
            // update settings
            var internalPath = Utilities.GetInternalDataPath();
            var list = new List<PDFReportInfo>();
            foreach (var data in settings.WorkingFolderToInternalFolderName)
            {
                var uuid = data.Value;
                var path = Path.Combine(internalPath, uuid, Constants.ReportSavedDataFileName);
                var json = File.ReadAllText(path);
                var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
                var report = File.Exists(path) ? JsonSerializer.Deserialize(json, jsonContext.PDFReport) : null;
                var reportTitle = report?.Title ?? "";
                var lastSaved = report?.LastSaved;
                var reportInfo = new PDFReportInfo()
                {
                    Title = reportTitle,
                    UUID = uuid,
                    LastSaved = lastSaved,
                    BaseFolder = data.Key,
                };
                // if UUID exists in BaseFolder/(Constants.ReportSavedDataFileName), use that UUID instead.
                var existingReportDataPath = Path.Combine(reportInfo.BaseFolder, Constants.ReportSavedDataFileName);
                if (File.Exists(existingReportDataPath))
                {
                    var originalReportData = JsonSerializer.Deserialize(File.ReadAllText(existingReportDataPath), jsonContext.PDFReport);
                    if (originalReportData != null)
                    {
                        if (!string.IsNullOrWhiteSpace(originalReportData.UUID))
                        {
                            Directory.Move(Path.Combine(internalPath, uuid), Path.Combine(internalPath, originalReportData.UUID));
                            reportInfo.UUID = originalReportData.UUID;
                        }
                        else
                        {
                            // update UUID so they are in sync between internal and external folders
                            originalReportData.UUID = reportInfo.UUID;
                            using var memoryStream = new MemoryStream();
                            JsonSerializer.Serialize(memoryStream, report, jsonContext.PDFReport);
                            memoryStream.Position = 0;
                            using var reader = new StreamReader(memoryStream);
                            var updatedJson = reader.ReadToEnd();
                            File.WriteAllText(existingReportDataPath, updatedJson);
                        }
                    }
                }
                list.Add(reportInfo);
            }
            settings.AllReportInfo = list.OrderBy(x => x.Title).ToList();
            settings.WorkingFolderToInternalFolderName = []; // clear this list; it is no longer going to be used
            settings.SettingsVersion = 2;
            settings.SaveSettingsNotAsync(); // saves all data; UUIDs should be in sync if user has toggled settings
        }
        return settings;
    }

    public static Settings LoadSettings()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new Settings();
        }
        var json = File.ReadAllText(GetSettingsPath());
        var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        return UpgradeSettings(JsonSerializer.Deserialize<Settings>(json, jsonContext.Settings) ?? new Settings());
    }

    public static async Task<Settings> LoadSettingsAsync()
    {
        using FileStream fileStream = File.OpenRead(GetSettingsPath());
        var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        var output = await JsonSerializer.DeserializeAsync<Settings>(fileStream, jsonContext.Settings) ?? new Settings();
        return UpgradeSettings(output);
    }
}