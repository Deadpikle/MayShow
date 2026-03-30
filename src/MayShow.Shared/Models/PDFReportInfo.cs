using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MayShow.Helpers;
using MayShows.Helpers;

namespace MayShow.Models;

class PDFReportInfo : ChangeNotifier
{
    private string? _baseFolder; // might be null; if set, use this to know where (initial) working dir is
    private string _uuid;
    private string _title;
    private DateTime? _lastSaved;

    public PDFReportInfo() : base()
    {
        _baseFolder = null;
        _uuid = Guid.NewGuid().ToString();
        _title = "";
        _lastSaved = null;
    }

    public string? BaseFolder
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

    public void ResetUUID()
    {
        UUID = Guid.NewGuid().ToString();
    }

    public string GetWorkingPath()
    {
        if (string.IsNullOrWhiteSpace(BaseFolder))
        {
            return Path.Combine(Utilities.GetInternalDataPath(), UUID);
        }
        return BaseFolder;
    }
    
    public void DeleteInternalFolderFromDisk()
    {
        var path = Path.Combine(Utilities.GetInternalDataPath(), UUID);
        if (Directory.Exists(path) && path != Utilities.GetInternalDataPath())
        {
            Directory.Delete(path, true);
        }
    }
}