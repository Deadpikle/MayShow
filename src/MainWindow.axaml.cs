using Avalonia.Controls;
using MayShow.Interfaces;
using MayShow.ViewModels;

namespace MayShow;

public partial class MainWindow : Window, ITopLevelGrabber
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(this);
    }

    public TopLevel GetTopLevel()
    {
        return this;
    }
}