using DialogHostAvalonia;
using MayShow.Helpers;
using MayShow.Models;

namespace MayShow.ViewModels;

class WarningDeleteItemViewModel : ChangeNotifier
{
    ReportFile _file;

    public WarningDeleteItemViewModel(ReportFile file)
    {
        _file = file;
    }

    public ReportFile File
    {
        get => _file;
    }

    public void KeepItem()
    {
        DialogHost.Close("DialogHost", false);
    }

    public void RemoveItem()
    {
        DialogHost.Close("DialogHost", true);
    }
}
