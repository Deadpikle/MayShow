using System;
using System.IO;
using System.Text.Json.Serialization;
using MayShow.Helpers;

namespace MayShow.Models;

class ReportFile : ChangeNotifier
{
    private string _title;
    private DateTime _receiptDateTime;
    private string _notes;
    private string _filePath;
    private int _indexInReport;
    private bool _isLastInReport;

    public ReportFile() : base()
    {
        _title = "";
        _receiptDateTime = DateTime.Now;
        _notes = "";
        _filePath = "";
        _indexInReport = 0;
        _isLastInReport = false;
    }

    public ReportFile(ReportFile other)
    {
        Title = _title = other.Title;
        ReceiptDateTime = _receiptDateTime = other.ReceiptDateTime ?? DateTime.Now;
        Notes = _notes = other.Notes;
        FilePath = _filePath = other.FilePath;
        _indexInReport = other.IndexInReport;
        _isLastInReport = other.IsLastInReport;
    }

    public string Title
    {
        get => _title;
        set { _title = value; NotifyPropertyChanged(); }
    }

    public DateTime? ReceiptDateTime
    {
        get => _receiptDateTime;
        set 
        {
            _receiptDateTime = value ?? DateTime.Now; 
            NotifyPropertyChanged(); 
            NotifyPropertyChanged(nameof(ReceiptDate));
        }
    }

    [JsonIgnore]
    public DateOnly ReceiptDate
    {
        get => DateOnly.FromDateTime(_receiptDateTime);
    }

    public string Notes
    {
        get => _notes;
        set { _notes = value; NotifyPropertyChanged(); }
    }

    public string FilePath
    {
        get => _filePath;
        set 
        { 
            _filePath = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(FileName));
            NotifyPropertyChanged(nameof(IsFileFoundOnDisk));
        }
    }

    [JsonIgnore]
    public string FileName
    {
        get => Path.GetFileName(_filePath);
    }

    [JsonIgnore]
    public bool IsFileFoundOnDisk
    {
        get
        {
            #if IOS
            // on iOS, files are there until we delete them unless they get corrupted on disk
            // in which case we have other issues!
            return true;
            #else
            return File.Exists(FilePath);
            #endif
        }
    }

    [JsonIgnore]
    public int IndexInReport
    {
        get => _indexInReport;
        set 
        { 
            _indexInReport = value; 
            NotifyPropertyChanged(); 
            NotifyPropertyChanged(nameof(IsFirstInReport)); 
        }
    }

    [JsonIgnore]
    public bool IsFirstInReport
    {
        get => _indexInReport == 0;
    }

    [JsonIgnore]
    public bool IsLastInReport // ...this is not pretty code, but oh well.
    {
        get => _isLastInReport;
        set { _isLastInReport = value; NotifyPropertyChanged(); }
    }
}