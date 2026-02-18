using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using ReceiptPDFBuilder.Models;
using ReceiptPDFBuilder.ViewModels;

namespace ReceiptPDFBuilder.Helpers;

class DataGridDropHandler : BaseDataGridDropHandler<ReportFile>
{
    // https://wieslawsoltes.github.io/Xaml.Behaviors/articles/drag-and-drop-datagrid/datagrid-drag-and-drop-overview.html
    // ...that page's code basically doesn't work in the way you'd expect. so just use the source code sample's code.
    protected override bool Validate(DataGrid dg, DragEventArgs e, object? sourceContext, object? targetContext, bool execute)
    {
        if (sourceContext is not ReportFile sourceItem
            || targetContext is not MainViewModel vm
            || dg.GetVisualAt(e.GetPosition(dg)) is not Control targetControl
            || targetControl.DataContext is not ReportFile targetItem)
        {
            return false;
        }

        var items = dg.ItemsSource as ObservableCollection<ReportFile>;
        return RunDropAction(dg, e, execute, sourceItem, targetItem, items?? []);
    }

    protected override ReportFile MakeCopy(ObservableCollection<ReportFile> parentCollection, ReportFile item)
    {
        // Return a clone of the item if you support Copy operations
        return new ReportFile { };
    }
}