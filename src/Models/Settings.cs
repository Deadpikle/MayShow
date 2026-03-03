using System;
using System.IO;
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

    public Settings()
    {
        _lastUsedPath = "";
        _useDocnetPDFImageRendering = true;
        _saveOutputPdfInWorkingDir = true;
        _outputPdfDir = "";
        _imageResizeThreshold = 1.5m;
    }

    public Settings(Settings other)
    {
        _lastUsedPath = other.LastUsedPath;
        _useDocnetPDFImageRendering = other.UseDocnetPDFImageRendering;
        _saveOutputPdfInWorkingDir = other.SaveOutputPdfInWorkingDir;
        _outputPdfDir = other.OutputPdfDir;
        _imageResizeThreshold = other.ImageResizeThreshold;
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

    public static string GetSettingsFileName()
    {
        return "settings.json";
    }

    public static string GetSettingsPath()
    {
        var path = Utilities.GetInternalDataPath();
        return Path.Combine(path, GetSettingsFileName());
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

    public static Settings LoadSettings()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new Settings();
        }
        var json = File.ReadAllText(GetSettingsPath());
        var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        return JsonSerializer.Deserialize<Settings>(json, jsonContext.Settings) ?? new Settings();
    }

    public static async Task<Settings> LoadSettingsAsync()
    {
        using FileStream fileStream = File.OpenRead(GetSettingsPath());
        var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        var output = await JsonSerializer.DeserializeAsync<Settings>(fileStream, jsonContext.Settings) ?? new Settings();
        return output;
    }
}