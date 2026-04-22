#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using DialogHostAvalonia;
using MayShow.Interfaces;
using MayShow.Models;
using MayShow.Helpers;
using System.Threading.Tasks;

namespace MayShow.ViewModels;

class StartNewChooseReportViewModel : BaseViewModel, ICanCheckShutdown, IUpdateRecentlyUsed
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

    public async void StartReport() // start a new report based on a title alone
    {
        if (string.IsNullOrWhiteSpace(CreatingReportTitle))
        {
            await DialogHost.Show(new WarningViewModel("Report title cannot be blank!"));
            return;
        }
        var reportInfo = new PDFReportInfo()
        {
            Title = CreatingReportTitle,
            LastSaved = null,
            UUID = Utilities.GetUniqueReportGuid(_settings).ToString()
        };
        reportInfo.UpdateBaseFolder();
        // now update UI
        ViewModelChanger.PushViewModel(new CreatePDFReportViewModel(reportInfo, ViewModelChanger)
        {
            UpdateRecentlyUsed = this,
            TopLevelGrabber = TopLevelGrabber
        });
        CreatingReportTitle = ""; // when user comes back they can start another new report
    }

    public void LoadExistingReport(object info) => LoadExistingReportImpl((PDFReportInfo) info);
    public void LoadExistingReportImpl(PDFReportInfo reportInfo)
    {
        ViewModelChanger.PushViewModel(new CreatePDFReportViewModel(reportInfo, ViewModelChanger)
        {
            UpdateRecentlyUsed = this,
            TopLevelGrabber = TopLevelGrabber
        });
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
    
    public void ShowAbout()
    {
        DialogHost.Show(new AboutViewModel());
    }

    public async Task ShowSettings()
    {
        var updatedSettings = await DialogHost.Show(new SettingsViewModel(_settings, TopLevelGrabber));
        if (updatedSettings != null)
        {
            _settings = (Settings)updatedSettings;
            await _settings.SaveSettingsAsync();
        }
    }

    public async Task<bool> CheckIsSafeToShutdown()
    {
        return true;
    }

    public async void UpdateRecentlyUsed(PDFReport report)
    {
        var didFind = false;
        foreach (var existing in _settings.AllReportInfo)
        {
            if (existing.UUID == report.UUID)
            {
                didFind = true;
                // update info on existing object
                existing.LastSaved = report.LastSaved;
                existing.Title = report.Title;
                existing.BaseFolder = report.BaseFolder;
            }
        }
        if (!didFind)
        {
            _settings.AllReportInfo.Add(report);
        }
        // ... this sort and save is slow, technically, but we're not going to have millions of items here, so...
        SavedReports = new ObservableCollection<PDFReportInfo>(_settings.AllReportInfo.OrderBy(x => x.Title));
        await _settings.SaveSettingsAsync();
    }
}