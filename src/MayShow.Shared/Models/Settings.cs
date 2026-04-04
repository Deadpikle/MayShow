using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MayShow.Helpers;
using MayShows.Helpers;

namespace MayShow.Models;

class Settings : ChangeNotifier
{
    private string _lastUsedPath;
    private bool _useDocnetPDFImageRendering;
    private bool _saveOutputPdfInWorkingDir;
    private string _outputPdfDir;
    private decimal _imageResizeThreshold;
    private bool _saveReportJsonDataInInternalDir;
    private Dictionary<string, string> _workingFolderToInternalFolderName; // obsolete
    private List<PDFReportInfo> _allReportInfo;
    public int _settingsVersion;

    public Settings() : base()
    {
        _lastUsedPath = "";
        _useDocnetPDFImageRendering = true;
        _saveOutputPdfInWorkingDir = true;
        _outputPdfDir = "";
        _imageResizeThreshold = 1.5m;
        _saveReportJsonDataInInternalDir = false;
        _workingFolderToInternalFolderName = [];
        _allReportInfo = [];
        _settingsVersion = 2;
    }

    public Settings(Settings other)
    {
        _lastUsedPath = other.LastUsedPath;
        _useDocnetPDFImageRendering = other.UseDocnetPDFImageRendering;
        _saveOutputPdfInWorkingDir = other.SaveOutputPdfInWorkingDir;
        _outputPdfDir = other.OutputPdfDir;
        _imageResizeThreshold = other.ImageResizeThreshold;
        _saveReportJsonDataInInternalDir = other.SaveReportJsonDataInInternalDir;
        _workingFolderToInternalFolderName = other.WorkingFolderToInternalFolderName;
        _settingsVersion = other.SettingsVersion;
        _allReportInfo = other.AllReportInfo;
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
    public bool SaveReportJsonDataInInternalDir
    {
        get => _saveReportJsonDataInInternalDir;
        set { _saveReportJsonDataInInternalDir = value; NotifyPropertyChanged(); }
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
                list.Add(reportInfo);
            }
            settings.AllReportInfo = list.OrderBy(x => x.Title).ToList();
            settings.WorkingFolderToInternalFolderName = []; // clear this list; it is no longer used
            settings.SettingsVersion = 2;
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