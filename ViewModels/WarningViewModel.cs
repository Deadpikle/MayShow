#nullable enable

using DialogHostAvalonia;

namespace ReceiptPDFBuilder.ViewModels;

class WarningViewModel
{
    private string _error;

    public WarningViewModel(string error)
    {
        _error = error;
    }

    public string Error
    {
        get => _error;
    }

    public void Close()
    {
        DialogHost.Close("DialogHost", null);
    }
}