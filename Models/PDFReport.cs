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
    private string _name;
    private List<ReportFile> _files;

    public PDFReport()
    {
        _baseFolder = "";
        _name = "";
        _files = [];
    }

    public string BaseFolder
    {
        get => _baseFolder;
        set { _baseFolder = value; NotifyPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; NotifyPropertyChanged(); }
    }

    public List<ReportFile> Files
    {
        get => _files;
        set { _files = value; NotifyPropertyChanged(); }
    }
}