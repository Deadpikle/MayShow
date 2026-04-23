using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MayShow.Enums;
using MayShow.Helpers;

namespace MayShow.Models;

class Settings : ChangeNotifier
{
    private string _lastUsedPath;
    private bool _useDocnetPDFImageRendering;
    private bool _saveOutputPdfInWorkingDir; // obsolete
    private string _outputPdfDir;
    private decimal _imageResizeThreshold;
    private Dictionary<string, string> _workingFolderToInternalFolderName; // obsolete
    private List<PDFReportInfo> _allReportInfo;
    public string _dataGridDateFormat;
    public string _reportDateFormat;
    public int _settingsVersion;
    private PDFSaveLocation _pdfOutputSaveLocation;

    public Settings() : base()
    {
        _lastUsedPath = "";
        _useDocnetPDFImageRendering = true;
        _saveOutputPdfInWorkingDir = true;
        _outputPdfDir = "";
        _imageResizeThreshold = 1.5m;
        _workingFolderToInternalFolderName = [];
        _allReportInfo = [];
        _settingsVersion = 3;
        _dataGridDateFormat = "dd/MM/yyyy";
        _reportDateFormat = "yyyy-MM-dd";
        _pdfOutputSaveLocation = PDFSaveLocation.BaseFolder;
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
        _pdfOutputSaveLocation = other.PDFOutputSaveLocation;
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
    public PDFSaveLocation PDFOutputSaveLocation
    {
        get => _pdfOutputSaveLocation;
        set { _pdfOutputSaveLocation = value; NotifyPropertyChanged(); }
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
                            Directory.Move(Path.Combine(internalPath, uuid), Path.Combine(internalPath, originalReportData.UUID));
                            reportInfo.UUID = originalReportData.UUID;
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
                reportInfo.BaseFolder = internalReportFolderPath;
                // make sure BaseFolder is set right just in case -- now always points to internal directory.
                // (it's actually now redundant because all settings are internal...
                // but for now we'll just let it stick around.)
                if (File.Exists(internalDataFilePath))
                {
                    var internalReportData = JsonSerializer.Deserialize(File.ReadAllText(internalDataFilePath), jsonContext.PDFReport);
                    if (internalReportData != null)
                    {
                        internalReportData.BaseFolder = internalReportFolderPath;
                        Utilities.SaveReportDataSync(internalReportData, internalDataFilePath, jsonContext.PDFReport);
                    }
                }
                // ok, finally done upgrading this report.
                list.Add(reportInfo);
            }
            settings.AllReportInfo = list.OrderBy(x => x.Title).ToList();
            settings.WorkingFolderToInternalFolderName = []; // clear this list; it is no longer going to be used
            settings.SettingsVersion = 2;
            settings.SaveSettingsNotAsync(); // saves all data; UUIDs should be in sync if user has toggled settings
        }
        if (settings.SettingsVersion == 2)
        {
            if (!settings.SaveOutputPdfInWorkingDir)
            {
                settings.PDFOutputSaveLocation = PDFSaveLocation.OtherChosenDir;
            }
            settings.SettingsVersion = 3;
            settings.SaveSettingsNotAsync();
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