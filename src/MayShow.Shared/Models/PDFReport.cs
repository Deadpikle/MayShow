using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MayShow.Helpers;

namespace MayShow.Models;

class PDFReport : PDFReportInfo
{
    private List<ReportFile> _files;
    private DateTime? _lastGenerated;

    public PDFReport() : base()
    {
        _files = [];
        _lastGenerated = null;
    }

    public List<ReportFile> Files
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