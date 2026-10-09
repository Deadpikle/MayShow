#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using DialogHostAvalonia;
using MayShow.Interfaces;
using MayShow.Models;
using MayShow.Helpers;
using System.Threading.Tasks;
using System.IO;
using System;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace MayShow.ViewModels;

class StartNewChooseReportViewModel : BaseViewModel, ICanCheckShutdown, IUpdateRecentlyUsed
{
    private string _creatingReportTitle;
    private ObservableCollection<PDFReport> _savedReports;
    private List<PDFReport> _savedReportsSearchResults;
    private Settings _settings;
    private string _previouslySavedSearchString;

    public StartNewChooseReportViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
    {
        _creatingReportTitle = "";
        _previouslySavedSearchString = "";
        _settings = Settings.LoadSettings();
        _settings.CleanupAbandonedFolders();
        _savedReports = 
            new ObservableCollection<PDFReport>(_settings.AllReportInfo.OrderBy(x => x.Title));
        _savedReportsSearchResults = _savedReports.ToList();
        #if IOS
        Console.WriteLine("Our internal data dir is: {0}", Utilities.GetInternalDataPath());
        #endif
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

    public string PreviouslySavedSearchString
    {
        get => _previouslySavedSearchString;
        set 
        { 
            _previouslySavedSearchString = value; 
            NotifyPropertyChanged(); 
            UpdateSearchResults();
        }
    }

    public ObservableCollection<PDFReport> SavedReports
    {
        get => _savedReports;
        set 
        { 
            _savedReports = value;
            NotifyPropertyChanged();
            UpdateSearchResults();
        }
    }

    public List<PDFReport> SavedReportsSearchResults
    {
        get => _savedReportsSearchResults;
        set { _savedReportsSearchResults = value; NotifyPropertyChanged(); }
    }

    private void UpdateSearchResults()
    {
        SavedReportsSearchResults = _savedReports
            .Where(x => x.Title.Contains(_previouslySavedSearchString, StringComparison.CurrentCultureIgnoreCase) || 
                (x.LastSaved?.ToString("yyyy-MM-dd")?.Contains(_previouslySavedSearchString, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (x.LastGenerated?.ToString("yyyy-MM-dd")?.Contains(_previouslySavedSearchString, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .ToList();
    }

    public async void StartReport() // start a new report based on a title alone
    {
        if (string.IsNullOrWhiteSpace(CreatingReportTitle))
        {
            await DialogHost.Show(new WarningViewModel("Report title cannot be blank!"));
            return;
        }
        var reportInfo = new PDFReport()
        {
            Title = CreatingReportTitle,
            LastSaved = null,
            UUID = Utilities.GetUniqueReportGuid(_settings).ToString()
        };
        reportInfo.SetBaseFolderToInternalWithUUID();
        // now update UI
        ViewModelChanger.PushViewModel(new CreatePDFReportViewModel(reportInfo, ViewModelChanger)
        {
            UpdateRecentlyUsed = this,
            TopLevelGrabber = TopLevelGrabber
        });
        CreatingReportTitle = ""; // when user comes back they can start another new report
    }

    public void LoadExistingReport(object info) => LoadExistingReportImpl((PDFReport) info);
    public void LoadExistingReportImpl(PDFReport reportInfo)
    {
        ViewModelChanger.PushViewModel(new CreatePDFReportViewModel(reportInfo, ViewModelChanger)
        {
            UpdateRecentlyUsed = this,
            TopLevelGrabber = TopLevelGrabber
        });
    }

    public void ShowPreviouslyGeneratedReportLocation(object info) => ShowPreviouslyGeneratedReportLocationImpl((PDFReport) info);

    private void ShowPreviouslyGeneratedReportLocationImpl(PDFReport reportInfo)
    {
        var topLevel = TopLevelGrabber?.GetTopLevel();
        if (topLevel != null && File.Exists(reportInfo.LastGeneratedBackupPath))
        {
            var lastGenPathDir = Path.GetDirectoryName(reportInfo.LastGeneratedBackupPath);
            if (!string.IsNullOrWhiteSpace(lastGenPathDir))
            {
                var launcher = topLevel.Launcher;
                launcher.LaunchUriAsync(new Uri(lastGenPathDir));
            }
        }
    }

    public void DeleteExistingReport(object info) => DeleteExistingReportImpl((PDFReport) info);
    public async void DeleteExistingReportImpl(PDFReport reportInfo)
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
            UpdateSearchResults();
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
                #if !IOS
                existing.BaseFolder = report.BaseFolder;
                #endif
                existing.LastGenerated = report.LastGenerated;
                existing.LastGeneratedBackupPath = report.LastGeneratedBackupPath;
            }
        }
        if (!didFind)
        {
            _settings.AllReportInfo.Add(report);
        }
        await ResortAndSaveSaveReports();
    }

    private async Task ResortAndSaveSaveReports()
    {
        // ... this sort and save is slow, technically, but we're not going to have millions of items here, so...
        SavedReports = new ObservableCollection<PDFReport>(_settings.AllReportInfo.OrderBy(x => x.Title));
        UpdateSearchResults();
        await _settings.SaveSettingsAsync();
    }

    public async void ImportExisting()
    {
        var topLevel = TopLevelGrabber?.GetTopLevel();
        if (topLevel is not null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Choose report_data.json file...",
                AllowMultiple = false,
                FileTypeFilter = [
                    new FilePickerFileType("All Types")
                    {
                        Patterns = ["*.json"],
                        AppleUniformTypeIdentifiers = [ "public.json" ],
                        MimeTypes = ["application/json"],
                    },
                ],
            });
            if (files.Count > 0)
            {
                var file = files[0];
                // read in file and import data as needed
                var reportInfo = Utilities.ImportReportDataJson(file.Path.LocalPath);
                if (reportInfo != null)
                {
                    _settings.AllReportInfo.Add(reportInfo);
                    await ResortAndSaveSaveReports();
                }
                else
                {
                    await DialogHost.Show(new WarningViewModel("Report file could not be imported. Was it created with a prior version of MayShow (v1.4.x or less)?"));
                }
            }
        }
    }
}