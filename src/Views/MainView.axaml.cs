using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MayShow.ViewModels;

namespace MayShow.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            this.InitializeComponent();
            LogBlock.PropertyChanged += LogBlock_PropertyChanged;
            FilesGrid.CellEditEnded += FileCellEditEnded;
        }

        private void LogBlock_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.ToString() == "Text")
            {
                LogScrollView.ScrollToEnd();
            }
        }

        public void UnfocusTextbox()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            topLevel?.FocusManager?.ClearFocus();
            if (DataContext is MainViewModel mvm)
            {
                mvm?.HasUnsavedWork = true;
            }
        }

        private void FileCellEditEnded(object? sender, DataGridCellEditEndedEventArgs args)
        {
            if (args.EditAction == DataGridEditAction.Commit && DataContext is MainViewModel mvm)
            {
                mvm?.HasUnsavedWork = true;
            }
        }
    }
}
