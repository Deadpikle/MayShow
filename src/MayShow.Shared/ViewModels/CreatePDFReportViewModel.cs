#nullable enable

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using DialogHostAvalonia;
using MayShow.Helpers;
using MayShow.Interfaces;
using MayShow.Models;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using MayShow.Enums;

namespace MayShow.ViewModels;

class CreatePDFReportViewModel : BaseViewModel, ICanCheckShutdown, ILogger
{
    #pragma warning disable CS0414
    private bool _isPerformingInitialLoad;
    #pragma warning restore CS0414
    private string _processDir;
    private string _programLog;
    private bool _isCreatingPDF;

    private PDFReport _pdfReport;

    private Settings _settings;
    private List<DateDisplayFormat> _dateDisplayFormats;
    private bool _hasUnsavedWork;
    private List<ReportFile> _deletedFiles;

    private CreatePDFReportViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
    {
        _pdfReport = new PDFReport();
        _processDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
        Console.WriteLine("Internal storage directory is: {0}", Utilities.GetInternalDataPath());
        _isCreatingPDF = false;
        ReportFiles = [];
        _programLog = "";
        _settings = Settings.LoadSettings();
        _dateDisplayFormats = Constants.GetDateDisplayFormats();
        NotifyPropertyChanged(nameof(DataGridDateFormat));
        NotifyPropertyChanged(nameof(DataGridDateFormatWatermark));
        HasUnsavedWork = false;
        _deletedFiles = [];
        // setup initial quote and program log data
        InitializeProgramLog();
    }

    public CreatePDFReportViewModel(PDFReport reportInfo, IChangeViewModel viewModelChanger) : this(viewModelChanger)
    {
        _isPerformingInitialLoad = true;
        PDFReport = reportInfo;
        PDFReport.LoadDataFileInfo(); // make sure file information is loaded
        _isPerformingInitialLoad = false;
    }

    public IUpdateRecentlyUsed? UpdateRecentlyUsed { get; set; }

    public PDFReport PDFReport
    {
        get => _pdfReport;
        set
        {
            _pdfReport = value;
            NotifyPropertyChanged(nameof(ReportTitle));
            NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled));
            NotifyPropertyChanged(nameof(ReportFiles));
            SetupFileCollectionChangedWatcher();
        }
    }

    public string ReportTitle
    {
        get => _pdfReport.Title;
        set 
        { 
            _pdfReport.Title = value;
            NotifyPropertyChanged();
            HasUnsavedWork = true;
        }
    }

    public bool CanAddItem
    {
        get => !IsCreatingPDF;
    }

    public bool IsCreatingPDF
    {
        get => _isCreatingPDF;
        set 
        { 
            _isCreatingPDF = value; 
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled)); 
            NotifyPropertyChanged(nameof(CanAddItem));
            NotifyPropertyChanged(nameof(IsSaveButtonAccentOn));
        }
    }

    public bool IsSaveButtonAccentOn
    {
        get => !_isCreatingPDF && HasUnsavedWork;
    }

    public bool IsCreatePDFButtonEnabled
    {
        get => !_isCreatingPDF && _pdfReport.Files.Count > 0;
    }

    public string ProgramLog
    {
        get => _programLog;
        set { _programLog = value; NotifyPropertyChanged(); }
    }

    public bool HasUnsavedWork
    {
        get => _hasUnsavedWork;
        set
        {
            _hasUnsavedWork = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(IsSaveButtonAccentOn));
        }
    }

    public ObservableCollection<ReportFile> ReportFiles
    {
        get => _pdfReport.Files;
        set 
        { 
            _pdfReport.Files = value;
            NotifyPropertyChanged();
            SetupFileCollectionChangedWatcher();
        }
    }

    public string DataGridDateFormat
    {
        get => _settings.DataGridDateFormat;
    }

    public string DataGridDateFormatWatermark
    {
        get => _dateDisplayFormats.FirstOrDefault(x => x.Value == _settings.DataGridDateFormat)?.Example ?? "2025-12-04";
    }

    private void SetupFileCollectionChangedWatcher()
    {
        _pdfReport.Files.CollectionChanged += ( sender, e ) => 
        { 
            Console.WriteLine("Coll changed");
            NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled));
            HasUnsavedWork = true;
        };
    }

    private void InitializeProgramLog()
    {
        var quotes = Constants.GetQuotes();
        var random = new Random();
        var quoteIndex = random.Next(0, quotes.Length);
        var compDetails = RuntimeInformation.OSDescription + " | " +
            RuntimeInformation.OSArchitecture.ToString();
        _programLog = "----- MayShow v" + Constants.AppVersion + " | " + compDetails + " ------" + Environment.NewLine;
        _programLog += quotes[quoteIndex] + Environment.NewLine;
        _programLog += "---------------------------------------" + Environment.NewLine;
        _programLog += "Loaded and ready to create report!" + Environment.NewLine;
        _programLog += "Please copy and send this Program Log when reporting any issues with the software.";
    }

    public void LogInfo(string message, params object[]? arguments)
    {
        var timestamp = string.Format("[{0:s}]", DateTime.Now);
        Console.WriteLine(timestamp + " " + message, arguments);
        ProgramLog += Environment.NewLine + string.Format(message, arguments ?? []);
    }

    // https://github.com/AvaloniaUI/Avalonia/issues/10075
    public void EditFileProperties(object f) => EditFilePropertiesImpl((ReportFile)f);
    public async void EditFilePropertiesImpl(ReportFile file)
    {
        var result = await DialogHost.Show(new EditFileViewModel(file, ViewModelChanger));
        if (result != null && result is ReportFile updatedData)
        {
            file.Title = updatedData.Title;
            file.ReceiptDateTime = updatedData.ReceiptDateTime;
            file.Notes = updatedData.Notes;
            HasUnsavedWork = true;
        }
    }

    public async void AddItem()
    {
        var topLevel = TopLevelGrabber?.GetTopLevel();
        if (topLevel is not null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Choose image or PDF files...",
                AllowMultiple = true,
                FileTypeFilter = Utilities.GetReportFilePickerFileTypes(),
            });
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    var filePath = file.TryGetLocalPath();
                    AddFileBasedOnPath(filePath);
                }
            }
        }
    }

    private void AddFileBasedOnPath(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) && !filePath.EndsWith(".DS_Store"))
        {
            // make sure extensions are OK
            var fileExtensions = Constants.AllowedFileExtensionsNoStar;
            var didMatch = false;
            foreach (var fileExtension in fileExtensions)
            {
                if (filePath.ToLower().EndsWith("." + fileExtension.ToLower()))
                {
                    didMatch = true;
                    break;
                }
            }
            if (!didMatch)
            {
                if (!filePath.EndsWith(Constants.ReportSavedDataFileName))
                {
                    LogInfo("File {0} did not match allowed file extension types, so it was not added.", filePath);
                }
            }
            else
            {
                var date = Utilities.CheckValidDateInString(filePath);
                if (_settings.CopyFilesToInternalDir)
                {
                    // copy file to internal folder, then add report file based on that path
                    // make sure file names are not conflicting with one another, too.
                    var fileName = Path.GetFileName(filePath);
                    var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
                    var extension = Path.GetExtension(filePath);
                    var copyToPath = Path.Combine(_pdfReport.BaseFolder, fileName);
                    if (!Directory.Exists(_pdfReport.BaseFolder))
                    {
                        Directory.CreateDirectory(_pdfReport.BaseFolder);
                    }
                    var rnd = new Random();
                    while (File.Exists(copyToPath))
                    {
                        fileName = fileNameNoExt + "-" + rnd.Next(1, 999_999) + extension;
                        copyToPath = Path.Combine(_pdfReport.BaseFolder, fileName);
                    }
                    File.Copy(filePath, copyToPath);
                    filePath = copyToPath;
                }
                ReportFiles.Add(new ReportFile()
                {
                    Title = Path.GetFileName(filePath),
                    ReceiptDateTime = date.HasValue ? date.Value.ToDateTime(TimeOnly.MinValue) : File.GetCreationTime(filePath),
                    Notes = "",
                    FilePath = filePath,
                });
                HasUnsavedWork = true;
            }
        }
    }

    public void RemoveFile(object f) => RemoveFileImpl((ReportFile)f);
    public async void RemoveFileImpl(ReportFile file)
    {
        var result = await DialogHost.Show(new WarningDeleteItemViewModel(file));
        if (result != null && (bool)result)
        {
            var idx = ReportFiles.IndexOf(file);
            if (idx != -1)
            {
                ReportFiles.RemoveAt(idx);
                NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled));
                _deletedFiles.Add(file);
                HasUnsavedWork = true;
            }
        }
    }

    public async void RemoveAllItems()
    {
        var result = await DialogHost.Show(new ConfirmViewModel("Warning!", "Are you sure you want to remove all items from this report?", "Remove All Items", "Cancel")
        {
            ConfirmButtonUsesDangerStyle = true,
            ConfirmTitleIcon = "\uf1f8;"
        });
        if (result != null && (bool)result)
        {
            foreach (var item in ReportFiles)
            {
                _deletedFiles.Add(item);
            }
            ReportFiles.Clear();
            HasUnsavedWork = true;
            NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled));
        }
    }

    public void LocateFile(object f) => LocateFileImpl((ReportFile)f);
    public async void LocateFileImpl(ReportFile reportFile)
    {
        var topLevel = TopLevelGrabber?.GetTopLevel();
        if (topLevel is not null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Choose image or PDF file...",
                AllowMultiple = false,
                FileTypeFilter = Utilities.GetReportFilePickerFileTypes(),
            });
            if (files.Count > 0)
            {
                var file = files[0];
                reportFile.FilePath = file.Path.LocalPath;
                HasUnsavedWork = true;
            }
        }
    }

    // https://github.com/AvaloniaUI/Avalonia/issues/10075
    public void OpenFile(object f) => OpenFileImpl((ReportFile)f);
    public void OpenFileImpl(ReportFile file)
    {
        var topLevel = TopLevelGrabber?.GetTopLevel();
        if (topLevel is not null)
        {
            var launcher = topLevel.Launcher;
            launcher.LaunchUriAsync(new Uri(file.FilePath));
        }
    }

    public void OpenFileLocation(object f) => OpenFileLocationImpl((ReportFile)f);

    private void OpenFileLocationImpl(ReportFile file)
    {
        OpenFolderForFileInFileViewer(file.FilePath);
    }

    private void OpenFolderForFileInFileViewer(string fullPathToFile)
    {
        var topLevel = TopLevelGrabber?.GetTopLevel();
        var dirName = Path.GetDirectoryName(fullPathToFile);
        if (topLevel is not null && dirName != null)
        {
            var launcher = topLevel.Launcher;
            launcher.LaunchUriAsync(new Uri(dirName));
        }
    }

    public void ResortPDFItemsByDate()
    {
        LogInfo("Sorting report files list...");
        ReportFiles = new ObservableCollection<ReportFile>(
            ReportFiles.OrderBy(x => x.ReceiptDateTime)
                       .ThenBy(x => x.Title));
        HasUnsavedWork = true;
    }

    // called from UI button
    public async void BuildPDF()
    {
        if (string.IsNullOrWhiteSpace(ReportTitle))
        {
            await DialogHost.Show(new WarningViewModel("You must provide a report title!"));
        }
        else
        {
            try
            {
                var outputFilePath = await DeterminePDFSaveLocation();
                if (outputFilePath == null && _settings.PDFOutputSaveLocation != PDFSaveLocation.AlwaysAsk)
                {
                    // if always ask and output is null, they probably hit cancel.
                    await DialogHost.Show(new WarningViewModel("Error: Output file path could not be determined. Current save location set in settings: " + Enum.GetName(_settings.PDFOutputSaveLocation)));
                }
                else if (_settings.PDFOutputSaveLocation == PDFSaveLocation.OtherChosenDir &&
                    !Directory.Exists(_settings.OutputPdfDir))
                {
                    await DialogHost.Show(new WarningViewModel("Error: Output directory not found! Please adjust the application Settings before continuing. Output directory: " + _settings.OutputPdfDir));
                }
                else if (outputFilePath != null)
                {
                    await Task.Run(() => CreatePDF(outputFilePath));
                }
            } catch (Exception e)
            {
                LogInfo("PDF process failed! Reason: " + e.Message);
                if (e.StackTrace != null)
                {
                    LogInfo(e.StackTrace);
                }
                var otherException = e.InnerException;
                while (otherException != null)
                {
                    LogInfo(">> Inner exception: " + otherException.Message);
                    if (otherException.StackTrace != null)
                    {
                        LogInfo(otherException.StackTrace);
                    }
                    otherException = otherException.InnerException;
                }
                LogInfo("Please report this error to a programmer or fix the issue listed above.");
                IsCreatingPDF = false;
            }
        }
    }

    public async Task SaveInterimReportInfo()
    {
        _pdfReport.LastSaved = DateTime.Now;
        await SavePDFReportDataToDisk(_pdfReport);
    }

    private async Task CreateAndSaveReportObjectAfterReportCreation()
    {
        _pdfReport.LastSaved = DateTime.Now;
        _pdfReport.LastGenerated = DateTime.Now;
        await SavePDFReportDataToDisk(_pdfReport);
    }

    private async Task SavePDFReportDataToDisk(PDFReport report)
    {
        var savePath = _pdfReport.GetReportSavedDataPath();
        await Utilities.SaveReportDataAsync(report, savePath);
        LogInfo("Saved report information to {0}", savePath);
        HasUnsavedWork = false;
        UpdateRecentlyUsed?.UpdateRecentlyUsed(report);
        foreach (var item in _deletedFiles)
        {
            // any items that have been deleted off the report
            // that are internal to the report should also be deleted
            // off of disk
            if (item.FilePath.StartsWith(_pdfReport.BaseFolder) && !Directory.Exists(item.FilePath) /* sanity check */)
            {
                File.Delete(item.FilePath);
            }
        }
        _deletedFiles.Clear();
    }

    // called from UI button
    public async Task CopyLogToClipboard()
    {
        var clipboard = TopLevelGrabber?.GetTopLevel().Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(ProgramLog);
            LogInfo("Program log has been copied to the clipboard!");
        }
    }

    private async Task<string?> DeterminePDFSaveLocation()
    {
        var fileName = ReportTitle + ".pdf";
        switch (_settings.PDFOutputSaveLocation)
        {
            case PDFSaveLocation.BaseFolder:
                return Path.Combine(_pdfReport.BaseFolder, fileName);
            case PDFSaveLocation.AlwaysAsk:
                Func<Task<string?>> getSaveFilePath = async () =>
                {
                    var topLevel = TopLevelGrabber?.GetTopLevel();
                    if (topLevel != null)
                    {
                        var result = await topLevel.StorageProvider.SaveFilePickerWithResultAsync(new FilePickerSaveOptions
                        {
                            Title = "Choose PDF save location...",
                            FileTypeChoices = [FilePickerFileTypes.Pdf],
                            SuggestedFileType = FilePickerFileTypes.Pdf,
                            DefaultExtension = "pdf",
                            ShowOverwritePrompt = true,
                            SuggestedFileName = ReportTitle + ".pdf"
                        });

                        if (result.File != null)
                        {
                            // Console.WriteLine("1: {0}", result.File.Path.AbsolutePath); // HTML escaped?!
                            // Console.WriteLine("2: {0}", result.File.Path.AbsoluteUri); // starts with file://
                            // Console.WriteLine("3: {0}", result.File.Path.LocalPath); // path for OS
                            // Console.WriteLine("4: {0}", result.File.Path.OriginalString); // starts with file://
                            // Console.WriteLine("5: {0}", result.File.Path.UserEscaped); // bool
                            // Console.WriteLine("6: {0}", result.File.Name); // just file name, no path
                            var path = result.File.Path.LocalPath;
                            if (!path.EndsWith(".pdf"))
                            {
                                // should be fine, but juuuust in case...
                                path += ".pdf";
                            }
                            // Console.WriteLine(path);
                            return path;
                        }
                    }
                    return null;
                };
                // must invoke on UI thread because getting file picker
                return await Dispatcher.UIThread.InvokeAsync(getSaveFilePath);
            case PDFSaveLocation.OtherChosenDir:
                return Path.Combine(_settings.OutputPdfDir, fileName);
        }
        return null;
    } 

    private async Task CreatePDF(string outputFilePath)
    {
        IsCreatingPDF = true;
        var reportCreator = new ReportPDFCreator(this);
        var outputPdfFile = await reportCreator.CreatePDF(ReportFiles.ToList(), ReportTitle, outputFilePath, new PDFFontResolver(_processDir, this), _settings);
        if (!string.IsNullOrWhiteSpace(outputPdfFile))
        {
            // backup PDF file just in case
            var backupFilePath = Utilities.GetPDFBackupDataPath();
            var backupFilePathAndName = Path.Combine(backupFilePath, ReportTitle + 
                " - Generated on " + DateTime.Now.ToString("yyyy-MM-dd \\a\\t HH-mm-ss") + ".pdf");
            File.Copy(outputFilePath, backupFilePathAndName);
            // if there is a prior backup, remove it
            if (_pdfReport.LastGeneratedBackupPath != null && File.Exists(_pdfReport.LastGeneratedBackupPath))
            {
                File.Delete(_pdfReport.LastGeneratedBackupPath);
            }
            _pdfReport.LastGeneratedBackupPath = backupFilePathAndName;
            // save report data automatically for user
            await CreateAndSaveReportObjectAfterReportCreation();
            OpenFolderForFileInFileViewer(outputPdfFile);
        }
        IsCreatingPDF = false;
    }

    public async void ReturnToMainMenu()
    {
        if (await CheckIsSafeToShutdown())
        {
            PopViewModel();
        }
    }

    public async Task<bool> CheckIsSafeToShutdown()
    {
        if (!HasUnsavedWork)
        {
            return true;
        }
        else
        {
            var result = await DialogHost.Show(new ShutdownCheckViewModel());
            if (result != null && result is ShutdownCheckOptions opt)
            {
                if (opt == ShutdownCheckOptions.SaveAndShutdown)
                {
                    await SaveInterimReportInfo();
                    return true;
                }
                else if (opt == ShutdownCheckOptions.NoSaveShutdown)
                {
                    return true;
                }
                else if (opt == ShutdownCheckOptions.CancelShutdown)
                {
                    return false;
                }
            }
        }
        return false;
    }
}