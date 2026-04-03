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

namespace MayShow.ViewModels;

class CreatePDFReportViewModel : BaseViewModel, ICanCheckShutdown, ILogger
{
    private bool _isPerformingInitialLoad;
    private string _processDir;
    private bool _isCreatingPDF;
    private string _programLog = "";
    private string _workingFolder;

    private string _reportTitle;
    private ObservableCollection<ReportFile> _reportFiles;
    private DateTime? _lastGeneratedTime;

    private Settings _settings;

    private bool _hasUnsavedWork;

    private CreatePDFReportViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
    {
        _processDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
        Console.WriteLine("Internal storage directory is: {0}", Utilities.GetInternalDataPath());
        _isCreatingPDF = false;
        _workingFolder = "";
        ReportFiles = _reportFiles = new ObservableCollection<ReportFile>();
        _reportTitle = "";
        _lastGeneratedTime = null;
        _settings = Settings.LoadSettings(); // TODO: needs tweaking
        HasUnsavedWork = false;
        // setup initial quote and program log data
        InitializeProgramLog();
    }

    // this is the "normal path" into the pdf report view
    // pathToLoad is presumably _settings.LastUsedPath but doesn't have to be
    public CreatePDFReportViewModel(string pathToLoad, IChangeViewModel viewModelChanger) : this(viewModelChanger)
    {
        _isPerformingInitialLoad = true;
        // TODO: load settings properly
        if (!string.IsNullOrWhiteSpace(pathToLoad))
        {
            LogInfo("Loading report data at path: {0}", pathToLoad);
            ScanFolder(pathToLoad);
        }
        else
        {
            LogInfo("Choose a receipt folder to begin...");
        }
        _isPerformingInitialLoad = false;
    }

    public CreatePDFReportViewModel(PDFReportInfo reportInfo, IChangeViewModel viewModelChanger) : this(viewModelChanger)
    {
        _isPerformingInitialLoad = true;
        // todo: load settings properly!
        // if BaseFolder set, regardless of where the JSON data is saved,
        // the working data (pictures, etc.) is outside of the current, working folder
        if (!string.IsNullOrWhiteSpace(reportInfo.BaseFolder))
        {
            LogInfo("Loading report data at path: {0}", reportInfo.BaseFolder);
            ScanFolder(reportInfo.BaseFolder);
        }
        else
        {
            // load data file in internal dir + UUID
            var path = Path.Combine(Utilities.GetInternalDataPath(), reportInfo.UUID);
            if (Directory.Exists(path))
            {
                ScanFolder(path); // even if points internally will be A-OK
            }
            else
            {
                // TODO: error
            }
        }
        _isPerformingInitialLoad = false;
    }

    private void InitializeProgramLog()
    {
        var quotes = Constants.GetQuotes();
        var random = new Random();
        var quoteIndex = random.Next(0, quotes.Length);
        _programLog = "----- MayShow v" + Constants.AppVersion + " ------" + Environment.NewLine;
        _programLog += quotes[quoteIndex] + Environment.NewLine;
        _programLog += "---------------------------------------" + Environment.NewLine;
        _programLog += "Loaded and ready to create report!" + Environment.NewLine;
        _programLog += "Please copy and send this Program Log when reporting any issues with the software.";
    }

    public string ReportTitle
    {
        get => _reportTitle;
        set 
        { 
            _reportTitle = value;
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
        get => !_isCreatingPDF && _reportFiles.Count > 0;
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
        get => _workingFolder;
        set
        {
            _workingFolder = value;
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
        get => _reportFiles;
        set 
        { 
            _reportFiles = value;
            NotifyPropertyChanged(); 
            _reportFiles.CollectionChanged += ( sender, e ) => 
            { 
                NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled));
                HasUnsavedWork = true;
            };
        }
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

    private string GetReportSavedDataPath(string folderPath)
    {
        if (_settings.SaveReportJsonDataInInternalDir)
        {
            var internalPath = Utilities.GetInternalDataPath();
            if (!_settings.WorkingFolderToInternalFolderName.ContainsKey(folderPath))
            {
                var uuid = "";
                var potentialPath = "";
                var isDone = false;
                // make sure uuid not already used...just in case...because paranoia...
                do
                {
                    uuid = Guid.NewGuid().ToString();
                    potentialPath = Path.Combine(internalPath, uuid);
                    isDone = !Directory.Exists(potentialPath);
                } while (!isDone);
                // make internal dir -- using dir so we have option to copy data into dir later if needed
                // (if we ever implement a more robust report system where we keep all files)
                Directory.CreateDirectory(potentialPath);
                _settings.WorkingFolderToInternalFolderName[folderPath] = uuid;
                _settings.SaveSettingsNotAsync(); // save new key/value pair
            }
            return Path.Combine(
                internalPath, 
                _settings.WorkingFolderToInternalFolderName[folderPath], 
                Constants.ReportSavedDataFileName
            );
        }
        else
        {
            return Path.Combine(folderPath, Constants.ReportSavedDataFileName);
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
            var successfullyLoadedPriorReport = false;
            if (File.Exists(reportFilePath))
            {
                // load prior report
                var json = File.ReadAllText(reportFilePath);
                var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
                var report = JsonSerializer.Deserialize(json, jsonContext.PDFReport);
                if (report != null && report.Files.Count > 0)
                {
                    Console.WriteLine("Loading prior report data at {0}", reportFilePath);
                    ReportFiles = new ObservableCollection<ReportFile>(report.Files);
                    ReportTitle = report.Title;
                    WorkingFolder = report.BaseFolder ?? "";
                    _lastGeneratedTime = report.LastGenerated ?? null;
                    LogInfo("Reloaded report last saved at {0}", report.LastSaved);
                    successfullyLoadedPriorReport = true;
                }
            }
            if (!successfullyLoadedPriorReport)
            {
                // Scan folder for files and display in DataGrid
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

    public void LocateFile(object f) => LocateFileImpl((ReportFile) f);
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
                if (e.InnerException != null)
                {
                    LogInfo("Inner exception: " + e.InnerException.Message);
                    if (e.InnerException.StackTrace != null)
                    {
                        LogInfo(e.InnerException.StackTrace);
                    }
                }
                LogInfo("Please report this error to a programmer or fix the issue listed above.");
                IsCreatingPDF = false;
            }
        }
    }

    public async Task SaveInterimReportInfo()
    {
        var report = new PDFReport()
        {
            Title = ReportTitle,
            Files = ReportFiles.ToList(),
            BaseFolder = WorkingFolder,
            LastSaved = DateTime.Now,
            LastGenerated = _lastGeneratedTime,
        };
        await SavePDFReportDataToDisk(report);
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
    }

    private async Task CreateAndSaveReportObjectAfterReportCreation()
    {
        var report = new PDFReport()
        {
            Title = ReportTitle,
            Files = ReportFiles.ToList(),
            BaseFolder = WorkingFolder,
            LastSaved = DateTime.Now,
            LastGenerated = DateTime.Now,
        };
        _lastGeneratedTime = DateTime.Now;
        await SavePDFReportDataToDisk(report);
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