using MayShow.Helpers;

namespace MayShow.Models;

class DateDisplayFormat : ChangeNotifier
{
    private string _title;
    private string _example;
    private string _value;

    public DateDisplayFormat(string title, string example, string value)
    {
        _title = title;
        _example = example;
        _value = value;
    }

    public string Title
    {
        get => _title;
        set { _title = value; NotifyPropertyChanged(); }
    }

    public string Example
    {
        get => _example;
        set { _example = value; NotifyPropertyChanged(); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; NotifyPropertyChanged(); }
    }
}