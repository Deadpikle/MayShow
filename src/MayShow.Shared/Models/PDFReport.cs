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

class PDFReport : PDFReportInfo
{
    private ObservableCollection<ReportFile> _files;
    private DateTime? _lastGenerated;

    public PDFReport() : base()
    {
        _files = [];
        _lastGenerated = null;
    }

    public PDFReport(PDFReportInfo info) : base()
    {
        _files = [];
        _lastGenerated = null;
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
}