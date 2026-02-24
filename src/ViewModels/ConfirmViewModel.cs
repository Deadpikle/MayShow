#nullable enable

using DialogHostAvalonia;
using MayShow.Helpers;

namespace MayShow.ViewModels;

class ConfirmViewModel
{
    private string _title;
    private string _message;
    private string _confirmTitle;
    private string _declineTitle;

    public ConfirmViewModel(string title, string message, string confirmTitle = "Yes", string declineTitle = "No")
    {
        _title = title;
        _message = message;
        _confirmTitle = confirmTitle;
        _declineTitle = declineTitle;
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

    public void Confirm()
    {
        DialogHost.Close("DialogHost", true);
    }

    public void Decline()
    {
        DialogHost.Close("DialogHost", false);
    }
}