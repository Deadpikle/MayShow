using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DialogHostAvalonia;
using MayShow.Interfaces;
using MayShow.ViewModels;

namespace MayShow;

public partial class MainWindow : Window, ITopLevelGrabber
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(this);

        Closing += WindowIsClosing;

        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        // lifetime?.ShutdownRequested += ApplicationIsShuttingDown;
    }

    private async void WindowIsClosing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true; // async -> need to cancel immediately
        if (await CheckIfClosePossible())
        {
            Closing -= WindowIsClosing;
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            lifetime?.ShutdownRequested -= ApplicationIsShuttingDown;
            Close();
        }
    }

    private async void ApplicationIsShuttingDown(object? sender, ShutdownRequestedEventArgs e)
    {
        e.Cancel = true; // async -> need to cancel immediately
        if (await CheckIfClosePossible())
        {
            Closing -= WindowIsClosing;
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            lifetime?.ShutdownRequested -= ApplicationIsShuttingDown;
            lifetime?.TryShutdown();
        }
    }

    private async Task<bool> CheckIfClosePossible()
    {
        var canShutdown = true;
        if (DataContext is MainViewModel mvm)
        {
            if (mvm is ICanCheckShutdown canCheck)
            {
                canShutdown = await canCheck.CheckIsSafeToShutdown();
            }
            // only checking 1 level but for this app that is OK
            if (canShutdown && mvm.CurrentViewModel is ICanCheckShutdown currModel)
            {
                try
                {
                    canShutdown = await currModel.CheckIsSafeToShutdown();
                }
                catch (Exception)
                {
                    canShutdown = true;
                }
            }
        }
        return canShutdown;
    }

    public TopLevel GetTopLevel()
    {
        return this;
    }
}