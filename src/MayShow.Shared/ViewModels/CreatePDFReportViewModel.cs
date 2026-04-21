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
using MayShows.Helpers;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MayShow.ViewModels;

class CreatePDFReportViewModel : BaseViewModel, ICanCheckShutdown, ILogger
{
    private bool _isPerformingInitialLoad;
    private string _processDir;
    private string _programLog;
    private bool _isCreatingPDF;

    private PDFReport _pdfReport;

    private Settings _settings;
    private List<DateDisplayFormat> _dateDisplayFormats;
    private bool _hasUnsavedWork;

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
        // setup initial quote and program log data
        InitializeProgramLog();
    }

    // this is the "normal path" into the pdf report view
    // pathToLoad is presumably _settings.LastUsedPath but doesn't have to be
    // public CreatePDFReportViewModel(string pathToLoad, IChangeViewModel viewModelChanger) : this(viewModelChanger)
    // {
    //     _isPerformingInitialLoad = true;
    //     if (!string.IsNullOrWhiteSpace(pathToLoad))
    //     {
    //         LogInfo("Loading report data at path: {0}", pathToLoad);
    //         ScanFolder(pathToLoad);
    //     }
    //     else
    //     {
    //         LogInfo("Choose a receipt folder to begin...");
    //     }
    //     _isPerformingInitialLoad = false;
    // }

    public CreatePDFReportViewModel(PDFReportInfo reportInfo, IChangeViewModel viewModelChanger) : this(viewModelChanger)
    {
        _isPerformingInitialLoad = true;
        _pdfReport = new PDFReport(reportInfo);
        // always default to using BaseFolder, which will always be set in the general case
        if (!string.IsNullOrWhiteSpace(_pdfReport.BaseFolder))
        {
            LogInfo("Loading report data at path: {0}", _pdfReport.BaseFolder);
            ScanFolder(_pdfReport.BaseFolder);
        }
        else
        {
            // load data file in internal dir + UUID
            var path = Path.Combine(Utilities.GetInternalDataPath(), _pdfReport.UUID);
            if (Directory.Exists(path))
            {
                ScanFolder(path); // even if points entirely to internal folder, we will be A-OK loading here
            }
            else
            {
                LogInfo("Erorr loading report! Folder does not exist: {0}", path);
            }
        }
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
            NotifyPropertyChanged(nameof(WorkingFolder));
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
            NotifyPropertyChanged(nameof(IsTitleBoxVisible)); 
            NotifyPropertyChanged(nameof(CanAddItem)); 
        }
    }

    public bool IsTitleBoxVisible
    {
        get => !string.IsNullOrWhiteSpace(WorkingFolder);
    }

    public bool CanAddItem
    {
        get => IsTitleBoxVisible && !IsCreatingPDF;
    }

    public bool IsCreatingPDF
    {
        get => _isCreatingPDF;
        set 
        { 
            _isCreatingPDF = value; 
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled)); 
            NotifyPropertyChanged(nameof(HasWorkingFolderAndNotMakingPDF));
            NotifyPropertyChanged(nameof(CanAddItem)); 
        }
    }

    public bool IsCreatePDFButtonEnabled
    {
        get => !_isCreatingPDF && _pdfReport.Files.Count > 0;
    }

    public bool HasWorkingFolder
    {
        get => !string.IsNullOrWhiteSpace(WorkingFolder) && Directory.Exists(WorkingFolder);
    }

    public bool HasWorkingFolderAndNotMakingPDF
    {
        get => !string.IsNullOrWhiteSpace(WorkingFolder) && Directory.Exists(WorkingFolder) && !_isCreatingPDF;
    }

    public string WorkingFolder
    {
        get 
        {
            if (string.IsNullOrWhiteSpace(_pdfReport.BaseFolder))
            {
                return Path.Combine(Utilities.GetInternalDataPath(), _pdfReport.UUID);
            }
            else
            {
                return _pdfReport.BaseFolder;
            }
        }
        set
        {
            _pdfReport.BaseFolder = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HasWorkingFolder));
            NotifyPropertyChanged(nameof(HasWorkingFolderAndNotMakingPDF));
        }
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

    public async void ChooseFolder()
    {
        var topLevel = TopLevelGrabber?.GetTopLevel();
        if (topLevel is not null)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Pick a folder of files...",
                AllowMultiple = false,
            });
            if (folders.Count == 1)
            {
                var folder = folders[0];
                LogInfo("Clearing existing list and loading items in folder: " + folder.Path.LocalPath);
                ReportFiles.Clear();
                ScanFolder(folder.Path.LocalPath);
                _settings.LastUsedPath = folder.Path.LocalPath;
                await _settings.SaveSettingsAsync();
                ResortPDFItemsByDate();
                HasUnsavedWork = true;
            }
        }
    }

    private string GetReportSavedDataPath(string workingFolder)
    {
        if (_settings.SaveReportJsonDataInInternalDir)
        {
            var internalPath = Utilities.GetInternalDataPath();
            var internalReportDataDir = Path.Combine(internalPath, _pdfReport.UUID);
            if (!Directory.Exists(internalReportDataDir))
            {
                Directory.CreateDirectory(internalReportDataDir);
            }
            return Path.Combine(internalReportDataDir, Constants.ReportSavedDataFileName);
        }
        else
        {
            return Path.Combine(workingFolder, Constants.ReportSavedDataFileName);
        }
    }

    private void ScanFolder(string path)
    {
        if (Directory.Exists(path))
        {
            WorkingFolder = path;
            NotifyPropertyChanged(nameof(IsTitleBoxVisible));
            NotifyPropertyChanged(nameof(CanAddItem));
            var reportFilePath = GetReportSavedDataPath(path);
            var successfullyLoadedPriorReportFile = false;
            if (File.Exists(reportFilePath))
            {
                // load prior report
                var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
                var report = JsonSerializer.Deserialize(File.ReadAllText(reportFilePath), jsonContext.PDFReport);
                if (report != null)
                {
                    PDFReport = report;
                    Console.WriteLine("Loading prior report data at {0}", reportFilePath);
                    LogInfo("Reloaded report last saved at {0}", report.LastSaved ?? DateTime.Now);
                    successfullyLoadedPriorReportFile = true;
                }
            }
            if (!successfullyLoadedPriorReportFile)
            {
                // Scan folder for files and display in DataGrid
                if (path != PDFReport.BaseFolder)
                {
                    // in this case, there is essentially no report existing, 
                    // so we need to make a new one.
                    PDFReport = new PDFReport()
                    {
                        Title = Path.GetDirectoryName(path) ?? "",
                        LastSaved = null,
                        UUID = Utilities.GetUniqueReportGuid(_settings).ToString(),
                        BaseFolder = path
                    };
                }
                ReportFiles.Clear();
                ReportTitle = "";
                var filePaths = Directory.GetFiles(WorkingFolder);
                foreach (var filePath in filePaths)
                {
                    AddFileBasedOnPath(filePath);
                }
                ResortPDFItemsByDate();
                HasUnsavedWork = true;
            }
        }
        else
        {
            LogInfo("Error: The directory {0} does not exist. Please select another folder.", path);
        }
        NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled));
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
            LogInfo("Saved updated settings!");
            NotifyPropertyChanged(nameof(DataGridDateFormat));
            NotifyPropertyChanged(nameof(DataGridDateFormatWatermark));
        }
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
                FileTypeFilter = [
                    new FilePickerFileType("All Types")
                    {
                        Patterns = Constants.AllowedFileExtensionPatterns,
                        AppleUniformTypeIdentifiers = [ "public.image", "com.adobe.pdf", "public.heic" ],
                        MimeTypes = [ "image/*", "application/pdf", "image/heic" ]
                    },
                    FilePickerFileTypes.ImageAll, 
                    new FilePickerFileType("HEIC Images")
                    {
                        Patterns = [ "*.heic" ],
                        AppleUniformTypeIdentifiers = [ "public.heic" ],
                        MimeTypes = [ "image/heic" ]
                    },
                    FilePickerFileTypes.Pdf,
                ],
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
                FileTypeFilter = [
                    new FilePickerFileType("All Types")
                    {
                        Patterns = Constants.AllowedFileExtensionPatterns,
                        AppleUniformTypeIdentifiers = [ "public.image", "com.adobe.pdf", "public.heic" ],
                        MimeTypes = [ "image/*", "application/pdf", "image/heic" ]
                    },
                    FilePickerFileTypes.ImageAll, 
                    new FilePickerFileType("HEIC Images")
                    {
                        Patterns = [ "*.heic" ],
                        AppleUniformTypeIdentifiers = [ "public.heic" ],
                        MimeTypes = [ "image/heic" ]
                    },
                    FilePickerFileTypes.Pdf,
                ],
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
                await Task.Run(() => CreatePDF(WorkingFolder));
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
        var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
        using var memoryStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memoryStream, report, jsonContext.PDFReport);
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream);
        var json = await reader.ReadToEndAsync();
        var savePath = GetReportSavedDataPath(WorkingFolder);
        await File.WriteAllTextAsync(savePath, json);
        LogInfo("Saved report information to {0}", savePath);
        HasUnsavedWork = false;
        UpdateRecentlyUsed?.UpdateRecentlyUsed(report);
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

    private async Task CreatePDF(string folderPath)
    {
        IsCreatingPDF = true;
        var reportCreator = new ReportPDFCreator(this);
        var outputPdfFile = await reportCreator.CreatePDF(ReportFiles.ToList(), ReportTitle, folderPath, new PDFFontResolver(_processDir, this), _settings);
        if (!string.IsNullOrWhiteSpace(outputPdfFile))
        {
            await CreateAndSaveReportObjectAfterReportCreation();
            OpenFolderForFileInFileViewer(outputPdfFile);
        }
        IsCreatingPDF = false;
    }

    public async Task<bool> CheckIsSafeToShutdown()
    {
        if (!HasUnsavedWork || string.IsNullOrWhiteSpace(WorkingFolder))
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