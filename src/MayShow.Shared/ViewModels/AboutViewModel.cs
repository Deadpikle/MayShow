using DialogHostAvalonia;

namespace MayShow.ViewModels;

class AboutViewModel
{
    public AboutViewModel()
    {
    }

    public void Close()
    {
        DialogHost.Close("DialogHost", null);
    }
}