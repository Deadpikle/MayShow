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

    public ReportFile()
    {
        _title = "";
        _receiptDateTime = DateTime.Now;
        _notes = "";
        _filePath = "";
    }

    public ReportFile(ReportFile other)
    {
        Title = _title = other.Title;
        ReceiptDateTime = _receiptDateTime = other.ReceiptDateTime ?? DateTime.Now;
        Notes = _notes = other.Notes;
        FilePath = _filePath = other.FilePath;
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
        get => File.Exists(FilePath);
    }
}