using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MayShow.Helpers;

namespace MayShow.Models;

class PDFReport : ChangeNotifier
{
    private string _baseFolder;
    private string _uuid;
    private string _title;
    private DateTime? _lastSaved;
    private DateTime? _lastGenerated;
    private string? _lastGeneratedBackupPath;
    private ObservableCollection<ReportFile> _files;

    public PDFReport()
    {
        _uuid = Guid.NewGuid().ToString();
        _baseFolder = "";
        SetBaseFolderToInternalWithUUID();
        _title = "";
        _lastSaved = null;
        _lastGenerated = null;
        _lastGeneratedBackupPath = null;
        _files = [];
    }

    public string BaseFolder
    {
        get => _baseFolder;
        set { _baseFolder = value; NotifyPropertyChanged(); }
    }

    public string UUID
    {
        get => _uuid;
        set { _uuid = value; NotifyPropertyChanged(); }
    }

    public string Title
    {
        get => _title;
        set { _title = value; NotifyPropertyChanged(); }
    }

    public DateTime? LastSaved
    {
        get => _lastSaved;
        set { _lastSaved = value; NotifyPropertyChanged(); }
    }

    public DateTime? LastGenerated
    {
        get => _lastGenerated;
        set { _lastGenerated = value; NotifyPropertyChanged(); }
    }

    public bool HasLastGenerated
    {
        get => File.Exists(LastGeneratedBackupPath);
    }

    public string? LastGeneratedBackupPath
    {
        get => _lastGeneratedBackupPath;
        set 
        { 
            _lastGeneratedBackupPath = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HasLastGenerated));
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public ObservableCollection<ReportFile> Files
    {
        get => _files;
        set { _files = value; NotifyPropertyChanged(); }
    }

    public void SetBaseFolderToInternalWithUUID()
    {
        _baseFolder = Path.Combine(Utilities.GetInternalDataPath(), _uuid);
    }

    public void ResetUUID()
    {
        UUID = Guid.NewGuid().ToString();
    }
    
    public void DeleteInternalFolderFromDisk()
    {
        var internalPath = Utilities.GetInternalDataPath();
        var path = Path.Combine(internalPath, UUID);
        if (Directory.Exists(path) && path != internalPath)
        {
            Directory.Delete(path, true);
        }
    }

    public string GetReportSavedDataPath()
    {
        if (!Directory.Exists(BaseFolder))
        {
            Directory.CreateDirectory(BaseFolder);
        }
        return Path.Combine(BaseFolder, Constants.ReportSavedDataFileName);
    }

    public string GetReportFileDataPath()
    {
        return Path.Combine(BaseFolder, Constants.ReportSavedFileInfoFileName);
    }

    /// <summary>
    /// Loads data file information and returns said data.
    /// Does NOT set internal <seealso cref="Files"/> member
    /// so that you are forced to set it externally so any
    /// collection watchers, etc. can be setup properly.
    /// </summary>
    /// <returns></returns>
    public ObservableCollection<ReportFile> GetDataFileInfo()
    {
        var dataFilePath = GetReportFileDataPath();
        if (File.Exists(dataFilePath))
        {
            var json = File.ReadAllText(dataFilePath);
            var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
            return JsonSerializer.Deserialize(json, jsonContext.ObservableCollectionReportFile) ?? [];
        }
        return [];
    }

    /// <summary>
    /// Assumes Files member has data in it
    /// </summary>
    public void SaveDataFileInfo()
    {
        var sourceGenContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        var context = sourceGenContext.ObservableCollectionReportFile;
        using var memoryStream = new MemoryStream();
        JsonSerializer.Serialize(memoryStream, Files, context);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        var json = reader.ReadToEnd();
        File.WriteAllText(GetReportFileDataPath(), json);
    }

    public async Task SaveDataFileInfoAsync()
    {
        var sourceGenContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        var context = sourceGenContext.ObservableCollectionReportFile;
        using var memoryStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryStream, Files, context);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        var json = await reader.ReadToEndAsync();
        await File.WriteAllTextAsync(GetReportFileDataPath(), json);
    }
}