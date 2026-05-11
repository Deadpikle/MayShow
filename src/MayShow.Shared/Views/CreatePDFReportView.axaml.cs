using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using MayShow.Enums;
using MayShow.Interfaces;
using MayShow.ViewModels;

namespace MayShow.Views;

#if IOS
public partial class CreatePDFReportView : UserControl, IGetUILocation
#else
public partial class CreatePDFReportView : UserControl
#endif
{
    public CreatePDFReportView()
    {
        DataContextChanged += DataContext_Changed;
        this.InitializeComponent();
        LogBlock.PropertyChanged += LogBlock_PropertyChanged;
        FilesGrid.CellEditEnded += FileCellEditEnded;
    }

    private void DataContext_Changed(object? sender, EventArgs e)
    {
        if (DataContext is CreatePDFReportViewModel vm)
        {
            vm.GetUILocation = this;
        }
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
        if (DataContext is CreatePDFReportViewModel mvm)
        {
            mvm?.HasUnsavedWork = true;
        }
    }

    private void FileCellEditEnded(object? sender, DataGridCellEditEndedEventArgs args)
    {
        if (args.EditAction == DataGridEditAction.Commit && DataContext is CreatePDFReportViewModel mvm)
        {
            mvm?.HasUnsavedWork = true;
        }
    }

    #if IOS
    Microsoft.Maui.Graphics.Rect IGetUILocation.GetUILocation(UIItem item)
    {
        if (item == UIItem.CreatePDFButton)
        {
            var transformedBounds = BuildPDFButton.GetTransformedBounds() ?? null;
            var loc = BuildPDFButton.PointToScreen(new Point(0,0));
            return new Microsoft.Maui.Graphics.Rect(loc.X, loc.Y, 
            transformedBounds?.Bounds.Width ?? 0, transformedBounds?.Bounds.Height ?? 0);
        }
        return Microsoft.Maui.Graphics.Rect.Zero;
    }
    #endif
}
