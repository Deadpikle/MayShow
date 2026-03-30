#nullable enable

using DialogHostAvalonia;
using MayShow.Helpers;

namespace MayShow.ViewModels;

class ConfirmViewModel : ChangeNotifier
{
    private string _title;
    private string _message;
    private string _confirmTitle;
    private string _declineTitle;
    private bool _confirmButtonUsesDangerStyle;
    private string _confirmButtonIcon;

    public ConfirmViewModel(string title, string message, string confirmTitle = "Yes", string declineTitle = "No")
    {
        _title = title;
        _message = message;
        _confirmTitle = confirmTitle;
        _declineTitle = declineTitle;
        _confirmButtonUsesDangerStyle = false;
        _confirmButtonIcon = "";
    }

    public string Title
    {
        get => _title;
    }

    public string Message
    {
        get => _message;
    }

    public string ConfirmTitle
    {
        get => _confirmTitle;
    }

    public string DeclineTitle
    {
        get => _declineTitle;
    }

    public bool ConfirmButtonIsAccent
    {
        get => !_confirmButtonUsesDangerStyle;
    }

    public bool ConfirmButtonIsDanger
    {
        get => _confirmButtonUsesDangerStyle;
    }

    public bool ConfirmButtonUsesDangerStyle
    {
        set
        {
            _confirmButtonUsesDangerStyle = value;
            NotifyPropertyChanged(nameof(ConfirmButtonIsAccent));
            NotifyPropertyChanged(nameof(ConfirmButtonIsDanger));
        }
    }

    public string ConfirmTitleIcon
    {
        get => _confirmButtonIcon;
        set
        {
            _confirmButtonIcon = value; NotifyPropertyChanged();            
        }
    }

    public void Confirm()
    {
        DialogHost.Close("DialogHost", true);
    }

    public void Decline()
    {
        DialogHost.Close("DialogHost", false);
    }
}