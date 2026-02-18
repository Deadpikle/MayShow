using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ReceiptPDFBuilder.Helpers;

namespace ReceiptPDFBuilder.Models;

class PDFReport : ChangeNotifier
{
    private string _baseFolder;
    private string _title;
    private List<ReportFile> _files;
    private DateTime _lastSaved;
    private DateTime? _lastGenerated;

    public PDFReport()
    {
        _baseFolder = "";
        _title = "";
        _files = [];
        _lastSaved = DateTime.Now;
        _lastGenerated = null;
    }

    public string BaseFolder
    {
        get => _baseFolder;
        set { _baseFolder = value; NotifyPropertyChanged(); }
    }

    public string Title
    {
        get => _title;
        set { _title = value; NotifyPropertyChanged(); }
    }

    public List<ReportFile> Files
    {
        get => _files;
        set { _files = value; NotifyPropertyChanged(); }
    }

    public DateTime LastSaved
    {
        get => _lastSaved;
        set { _lastSaved = value; NotifyPropertyChanged(); }
    }

    public DateTime? LastGenerated
    {
        get => _lastGenerated;
        set { _lastGenerated = value; NotifyPropertyChanged(); }
    }
}