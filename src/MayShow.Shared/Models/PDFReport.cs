using System;
using System.Collections.ObjectModel;

namespace MayShow.Models;

class PDFReport : PDFReportInfo
{
    private ObservableCollection<ReportFile> _files;
    private DateTime? _lastGenerated;
    private string? _lastGeneratedBackupPath;

    public PDFReport() : base()
    {
        _files = [];
        _lastGenerated = null;
        _lastGeneratedBackupPath = null;
    }

    public PDFReport(PDFReportInfo info) : base()
    {
        _files = [];
        _lastGenerated = null;
        _lastGeneratedBackupPath = null;
        BaseFolder = info.BaseFolder;
        UUID = info.UUID;
        Title = info.Title;
        LastSaved = info.LastSaved;
    }

    public ObservableCollection<ReportFile> Files
    {
        get => _files;
        set { _files = value; NotifyPropertyChanged(); }
    }

    public DateTime? LastGenerated
    {
        get => _lastGenerated;
        set { _lastGenerated = value; NotifyPropertyChanged(); }
    }

    public string? LastGeneratedBackupPath
    {
        get => _lastGeneratedBackupPath;
        set { _lastGeneratedBackupPath = value; NotifyPropertyChanged(); }
    }
}