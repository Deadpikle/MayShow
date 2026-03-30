#nullable enable

using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DialogHostAvalonia;
using MayShow.Interfaces;
using MayShow.Models;
using MayShow.Helpers;
using MayShows.Helpers;
using System;

namespace MayShow.ViewModels;

class StartNewChooseReportViewModel : BaseViewModel
{
    private string _creatingReportTitle;
    private ObservableCollection<PDFReportInfo> _savedReports;
    private Settings _settings;

    public StartNewChooseReportViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
    {
        _creatingReportTitle = "";
        _settings = Settings.LoadSettings();
        _savedReports = new ObservableCollection<PDFReportInfo>(_settings.AllReportInfo.OrderBy(x => x.Title));
    }

    public static string Version
    {
        get => Constants.AppVersion;
    }

    public string CreatingReportTitle
    {
        get => _creatingReportTitle;
        set { _creatingReportTitle = value; NotifyPropertyChanged(); }
    }

    public ObservableCollection<PDFReportInfo> SavedReports
    {
        get => _savedReports;
        set { _savedReports = value; NotifyPropertyChanged(); }
    }

    public async void StartReport()
    {
        var reportInfo = new PDFReportInfo()
        {
            Title = CreatingReportTitle,
            LastSaved = DateTime.Now
        };
        _settings.AllReportInfo.Add(reportInfo);
        // ... this sort and save is slow, technically, but we're not going to have millions of items here, so...
        SavedReports = new ObservableCollection<PDFReportInfo>(_settings.AllReportInfo.OrderBy(x => x.Title));
        await _settings.SaveSettingsAsync();
        // create folder for report data
        var path = Path.Combine(Utilities.GetInternalDataPath(), reportInfo.UUID);
        while (Directory.Exists(path))
        {
            reportInfo.ResetUUID();
            path = Path.Combine(Utilities.GetInternalDataPath(), reportInfo.UUID);
        }
        Directory.CreateDirectory(path);
        // now update UI
        ViewModelChanger.PushViewModel(new CreatePDFReportViewModel(ViewModelChanger)
        {
            ReportTitle = CreatingReportTitle
        });
        CreatingReportTitle = ""; // when user comes back they can start another new report
    }

    public void LoadExistingReport(object info) => LoadExistingReportImpl((PDFReportInfo) info);
    public void LoadExistingReportImpl(PDFReportInfo reportInfo)
    {
        // TODO: load data and send to create PDF report view model
    }

    public void DeleteExistingReport(object info) => DeleteExistingReportImpl((PDFReportInfo) info);
    public async void DeleteExistingReportImpl(PDFReportInfo reportInfo)
    {
        var message = string.IsNullOrWhiteSpace(reportInfo.BaseFolder)
            ? "Are you sure you want to delete this report and its associated data? It will be gone forever!"
            : "Are you sure you want to delete information about this report? It will be gone forever!";
        var result = await DialogHost.Show(new ConfirmViewModel(
            "Warning!", 
            message, 
            "Delete Report", 
            "Cancel")
        {
            ConfirmButtonUsesDangerStyle = true,
            ConfirmTitleIcon = "\uf1f8;"
        });
        if (result != null && (bool)result)
        {
            SavedReports.Remove(reportInfo);
            _settings.AllReportInfo.Remove(reportInfo);
            reportInfo.DeleteInternalFolderFromDisk(); // delete internal data if available
            await _settings.SaveSettingsAsync(); // update saved items list
        }
    }
}