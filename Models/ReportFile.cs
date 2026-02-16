using System;
using System.IO;
using ReceiptPDFBuilder.Helpers;

namespace ReceiptPDFBuilder.Models;

class ReportFile : ChangeNotifier
{
    private bool _isMoveUpEnabled = false;
    private bool _isMoveDownEnabled = false;

    private string _title;
    private DateOnly _date;
    private DateTime _dateTime;
    private DateTimeOffset _dto;
    private string _notes;
    private string _filePath;

    public ReportFile()
    {
        _title = "";
        _date = DateOnly.FromDateTime(DateTime.Now);
        _notes = "";
        _filePath = "";
    }

    public string Title
    {
        get => _title;
        set { _title = value; NotifyPropertyChanged(); }
    }

    public DateOnly Date
    {
        get => _date;
        set 
        { 
            _date = value; 
            DateTime = value.ToDateTime(TimeOnly.MinValue);
            DTO = new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue)); 
            NotifyPropertyChanged(); 
        }
    }

    public DateTime DateTime
    {
        get => _dateTime;
        set 
        { 
            _dateTime = value; 
            NotifyPropertyChanged(); 
        }
    }

    public DateTimeOffset DTO
    {
        get => _dto;
        set 
        { 
            _dto = value; 
            NotifyPropertyChanged(); 
        }
    }

    public string Notes
    {
        get => _notes;
        set { _notes = value; NotifyPropertyChanged(); }
    }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; NotifyPropertyChanged(); }
    }

    public string FileName
    {
        get => Path.GetFileName(_filePath);
    }

    public bool IsMoveUpEnabled
    {
        get => _isMoveUpEnabled;
        set { _isMoveUpEnabled = value; NotifyPropertyChanged(); }
    }

    public bool IsMoveDownEnabled
    {
        get => _isMoveDownEnabled;
        set { _isMoveDownEnabled = value; NotifyPropertyChanged(); }
    }
}