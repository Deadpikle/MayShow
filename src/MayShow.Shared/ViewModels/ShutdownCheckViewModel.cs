#nullable enable

using DialogHostAvalonia;
using MayShow.Models;

namespace MayShow.ViewModels;

class ShutdownCheckViewModel
{

    public ShutdownCheckViewModel()
    {
    }

    public void SaveAndShutdown()
    {
        DialogHost.Close("DialogHost", ShutdownCheckOptions.SaveAndShutdown);
    }

    public void DoNotSaveAndShutdown()
    {
        DialogHost.Close("DialogHost", ShutdownCheckOptions.NoSaveShutdown);
    }

    public void CancelShutdown()
    {
        DialogHost.Close("DialogHost", ShutdownCheckOptions.CancelShutdown);
    }
}