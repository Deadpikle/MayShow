using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ReceiptPDFBuilder.Helpers;
using ReceiptPDFBuilders.Helpers;

namespace ReceiptPDFBuilder.Models;

class Settings : ChangeNotifier
{
    private string _lastUsedPath;

    public Settings()
    {
        _lastUsedPath = "";
    }

    [JsonInclude]
    public string LastUsedPath
    {
        get => _lastUsedPath;
        set { _lastUsedPath = value; NotifyPropertyChanged(); }
    }

    public static string GetSettingsFileName()
    {
        return "settings.json";
    }

    public static string GetSettingsPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ReceiptPDFBuilder"
        );
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
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