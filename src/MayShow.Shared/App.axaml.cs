using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;
using MayShow;
using MayShow.Interfaces;
using MayShow.Views;
using MayShow.ViewModels;

namespace MayShow;

public partial class App : Application, ITopLevelGrabber
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private TopLevel? _topLevel;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            // singleViewPlatform.MainView = new MainView();
            // _topLevel = singleViewPlatform.MainView as TopLevel;
            //_topLevel = TopLevel.GetTopLevel(singleViewPlatform.MainView);
            //singleViewPlatform.MainView.DataContext = new MainViewModel(this);
        }

        base.OnFrameworkInitializationCompleted();
    }

    public TopLevel GetTopLevel()
    {
        return _topLevel;
    }

    public void AboutOnClick(object? sender, EventArgs args)
    {
        DialogHost.Show(new AboutViewModel());
    }
}