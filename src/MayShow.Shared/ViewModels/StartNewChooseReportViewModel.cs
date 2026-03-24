#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Themes.Fluent;
using DialogHostAvalonia;
using ImageMagick;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
using PdfSharp.Snippets.Font;
using MayShow.Interfaces;
using MayShow.Models;
using MayShow.Helpers;

namespace MayShow.ViewModels;

class StartNewChooseReportViewModel : BaseViewModel
{
    private string _creatingReportTitle;
    private ObservableCollection<string> _savedReports;

    public StartNewChooseReportViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
    {
        _creatingReportTitle = "";
        // TODO: load existing reports
        _savedReports = [];
        for (var i = 1; i <= 100; i++)
        {
            _savedReports.Add("Report " + i);
        }
    }

    public string Version
    {
        get => Constants.AppVersion;
    }

    public string CreatingReportTitle
    {
        get => _creatingReportTitle;
        set { _creatingReportTitle = value; NotifyPropertyChanged(); }
    }

    public ObservableCollection<string> SavedReports
    {
        get => _savedReports;
        set { _savedReports = value; NotifyPropertyChanged(); }
    }

    public void StartReport()
    {
        // TODO: make sure there is a folder and everything set up for this report
        ViewModelChanger.PushViewModel(new CreatePDFReportViewModel(ViewModelChanger)
        {
            ReportTitle = CreatingReportTitle
        });
        CreatingReportTitle = ""; // when user comes back they can start another new report
        // TODO: add to existing reports list
    }

    public void LoadExistingReport()
    {
        // TODO: load data and send to create PDF report view model
    }

    public void DeleteExistingReport()
    {
        // TODO: warn user, delete if they want to proceed
    }
}