#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
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
using MayShow.Helpers;
using MayShow.Interfaces;
using MayShow.Models;
using MayShows.Helpers;

using Docnet.Core.Models;
using Docnet.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Reflection.Metadata.Ecma335;
using Docnet.Core.Readers;

namespace MayShow.ViewModels;

class MainViewModel : BaseViewModel, IFontResolver, ICanCheckShutdown
{
    private bool _isPerformingInitialLoad;
    private string _processDir;
    private bool _isCreatingPDF;
    private string _programLog;
    private string _workingFolder;

    private string _reportTitle;
    private ObservableCollection<ReportFile> _reportFiles;
    private DateTime? _lastGeneratedTime;

    private Settings _settings;

    private bool _hasUnsavedWork;

    public MainViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
    {
        _isPerformingInitialLoad = true;
        _processDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
        Console.WriteLine("Internal storage directory is: {0}", Utilities.GetInternalDataPath());
        _isCreatingPDF = false;
        var quotes = Constants.GetQuotes();
        Random random = new Random();
        var quoteIndex = random.Next(0, quotes.Length);
        _programLog = "----- MayShow v" + Constants.AppVersion + " ------" + Environment.NewLine;
        _programLog += quotes[quoteIndex] + Environment.NewLine;
        _programLog += "---------------------------------------" + Environment.NewLine;
        _programLog += "Loaded and ready to create report!" + Environment.NewLine;
        _programLog += "Please copy and send this Program Log when reporting any issues with the software.";
        _workingFolder = "";
        ReportFiles = _reportFiles = new ObservableCollection<ReportFile>();
        _reportTitle = "";
        _lastGeneratedTime = null;
        _settings = Settings.LoadSettings();
        if (!string.IsNullOrWhiteSpace(_settings.LastUsedPath))
        {
            LogInfo("Loading data at last used path of {0}", _settings.LastUsedPath);
            ScanFolder(_settings.LastUsedPath);
        }
        else
        {
            LogInfo("Choose a receipt folder to begin...");
        }
        HasUnsavedWork = false;
        _isPerformingInitialLoad = false;
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

    private void LogInfo(string message, params object[]? arguments)
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
                var report = JsonSerializer.Deserialize<PDFReport>(json, jsonContext.PDFReport);
                if (report != null && report.Files.Count > 0)
                {
                    Console.WriteLine("Loading prior report data at {0}", reportFilePath);
                    ReportFiles = new ObservableCollection<ReportFile>(report.Files);
                    ReportTitle = report.Title;
                    WorkingFolder = report.BaseFolder;
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
        var result = await DialogHost.Show(new ConfirmViewModel("Warning!", "Are you sure you want to remove all items from this report?", "Remove All Items", "Cancel"));
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
        ReportFiles = new ObservableCollection<ReportFile>(ReportFiles.OrderBy(x => x.ReceiptDateTime));
        HasUnsavedWork = true;
    }

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

    public async Task CopyLogToClipboard()
    {
        var clipboard = TopLevelGrabber?.GetTopLevel().Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(ProgramLog);
            LogInfo("Program log has been copied to the clipboard!");
        }
    }

    public byte[]? GetFont(string faceName)
    {
        LogInfo(string.Format("Loading font {0}", faceName));
        if (faceName == "Noto Sans JP")
        {
            var path = Path.Combine(_processDir, "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf");
            if (!File.Exists(path))
            {
                path = Path.Combine(_processDir, "../Resources/Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf");
            }
            return File.ReadAllBytes(path);
        }
        if (faceName == "Noto Sans JP Bold")
        {
            var path = Path.Combine(_processDir, "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-SemiBold.ttf");
            if (!File.Exists(path))
            {
                path = Path.Combine(_processDir, "../Resources/Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-SemiBold.ttf");
            }
            return File.ReadAllBytes(path);
        }
        return null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        // LogInfo(string.Format("Resolving font name {0}", familyName));
        if (familyName == "Noto Sans JP")
        {
            if (bold)
            {
                return new FontResolverInfo(familyName + " Bold");
            }
            return new FontResolverInfo(familyName);
        }
        return null;
    }

    // https://forum.pdfsharp.net/viewtopic.php?f=2&t=1025
    private async Task CreatePDF(string folderPath)
    {
        // TODO: calculate needed width for images based on page width and margins and all that?
        // TODO: resize (non-HEIC) images for smaller size...?
        // safety checks
        var outputDir = _settings.SaveOutputPdfInWorkingDir ? folderPath : _settings.OutputPdfDir;
        if (!Directory.Exists(outputDir))
        {
            await DialogHost.Show(new WarningViewModel("Output directory not found! Please adjust your application Settings before continuing. Output directory: " + outputDir));
            return;
        }
        // start making PDF!
        IsCreatingPDF = true;
        var pdfDoc = new Document();
        var outputFileName = ReportTitle + ".pdf";
        var folderName = new DirectoryInfo(folderPath).Name;
        const int imageWidth = 425;
        if (folderName.Contains('-'))
        {
            // see if year/month format
            var parts = folderName.Split('-');
            if (parts[0].Length == 4 &&
                parts[1].Length <= 2 &&
                int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
            {
                outputFileName = string.Format("{0} {1} Receipts.pdf", 
                    CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month), 
                    year);
                LogInfo("Auto-changed output file name to " + outputFileName);
            }
        }
        var section = pdfDoc.AddSection();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.PageWidth = "8.5in";
        section.PageSetup.PageHeight = "11in";
        section.PageSetup.TopMargin = "0.5in";
        section.PageSetup.RightMargin = "0.5in";
        section.PageSetup.BottomMargin = "0.5in";
        section.PageSetup.LeftMargin = "0.5in";
        // setup footer for page number
        var footerPar = new Paragraph();
        footerPar.Format.Alignment = ParagraphAlignment.Center;
        footerPar.Format.Font.Size = 10;
        footerPar.AddText("--Page ");
        footerPar.AddPageField();
        footerPar.AddText(" of ");
        footerPar.AddNumPagesField();
        footerPar.AddText("--");
        footerPar.AddLineBreak();
        footerPar.AddText("Report generated on " + DateTime.Now.ToString("f"));
        section.Footers.Primary.Add(footerPar);
        // add report title
        var reportTitlePar = section.AddParagraph();
        reportTitlePar.Format.Alignment = ParagraphAlignment.Center;
        reportTitlePar.Format.Font.Size = 16;
        reportTitlePar.Format.Font.Bold = true;
        reportTitlePar.Format.Font.Name = "Noto Sans JP"; // has english letters in it, too
        reportTitlePar.AddText(ReportTitle);
        // get converted files directory path and create it if necessary
        var convertedDir = Path.Combine(Utilities.GetInternalDataPath(), "converted");
        if (!Directory.Exists(convertedDir))
        {
            Directory.CreateDirectory(convertedDir);
        }
        //
        GlobalFontSettings.FontResolver = this;
        GlobalFontSettings.FallbackFontResolver = new FailsafeFontResolver();
        var hasAddedData = false;
        for (var i = 0; i < ReportFiles.Count; i++)
        {
            var file = ReportFiles[i];
            var fileName = file.FileName;
            var filePath = file.FilePath;
            if (!File.Exists(filePath))
            {
                LogInfo("ERROR: File \"{0}\" does not exist at path \"{1}\". Please remove it from the report or re-add it using the Add Item button if you still want it to be in this report.", file.Title, file.FilePath);
                IsCreatingPDF = false;
                return;
            }
            if (fileName == ".DS_Store" || fileName == outputFileName)
            {
                continue;
            }
            if (i > 0 && hasAddedData)
            {
                section.AddPageBreak();
            }
            var imageTitlePar = section.AddParagraph();
            imageTitlePar.Format.Alignment = ParagraphAlignment.Center;
            imageTitlePar.Format.Font.Size = 12;
            imageTitlePar.Format.Font.Bold = true;
            imageTitlePar.Format.Font.Name = "Noto Sans JP"; // has english letters in it, too
            imageTitlePar.AddText(string.IsNullOrWhiteSpace(file.Title) ? file.FileName : file.Title);
            var receiptDatePar = section.AddParagraph();
            receiptDatePar.Format.Alignment = ParagraphAlignment.Center;
            receiptDatePar.Format.Font.Size = 12;
            receiptDatePar.Format.Font.Bold = true;
            receiptDatePar.Format.Font.Name = "Noto Sans JP"; // has english letters in it, too
            receiptDatePar.AddText(file.ReceiptDate.ToString("yyyy-MM-dd"));
            if (!string.IsNullOrWhiteSpace(file.Notes))
            {
                var imageNotesPar = section.AddParagraph();
                imageNotesPar.Format.Alignment = ParagraphAlignment.Center;
                imageNotesPar.Format.Font.Size = 10;
                imageNotesPar.Format.Font.Bold = false;
                imageNotesPar.Format.Font.Name = "Noto Sans JP";
                imageNotesPar.AddText(file.Notes);
            }
            section.AddParagraph(); // add empty line for spacing
            // now add the image
            var lowerName = fileName.ToLower();
            var isPDF = lowerName.EndsWith(".pdf");
            // convert heic, webp, or png to JPEG for size and ease of use
            // (and probably compat reasons too, though I haven't tested that...)
            var isHEIC = lowerName.EndsWith(".heic");
            var isWebp = lowerName.EndsWith(".webp");
            var isPNG = lowerName.EndsWith(".png");
            var info = new FileInfo(file.FilePath);
            uint loadedImageWidth = 0;
            uint loadedImageHeight = 0;
            if (!isPDF)
            {
                using var mImage = new MagickImage(info.FullName);
                loadedImageWidth = mImage.Width;
                loadedImageHeight = mImage.Height;
                var convertedOutputPath = Path.Combine(convertedDir, info.Name + ".jpg");
                var didAdjust = false;
                LogInfo("Image orientation of {0} is {1}", fileName, mImage.Orientation);
                if (mImage.Orientation != OrientationType.TopLeft)
                {
                    LogInfo("Auto-adjusted image orientation of {0}", fileName);
                    mImage.AutoOrient();
                    didAdjust = true;
                }
                // perform needed image manipulations
                if (isHEIC || isWebp || isPNG || (!isPDF && info.Length > _settings.ImageResizeThreshold * 1024 * 1024))
                {
                    // Save image as jpg
                    mImage.Quality = 80;
                    if (mImage.Width >= 400 || mImage.Height >= 400)
                    {
                        loadedImageWidth = (uint)Math.Floor(mImage.Width * 0.5);
                        loadedImageHeight = (uint)Math.Floor(mImage.Height * 0.5);
                        mImage.Scale(loadedImageWidth, loadedImageHeight);
                        LogInfo("Image {2} scaled to {0}x{1}", loadedImageWidth, loadedImageHeight, fileName);
                    }
                    didAdjust = true;
                    LogInfo("Converted image {0} to JPEG", fileName);
                }
                else
                {
                    // load height/width
                    loadedImageWidth = mImage.Width;
                    loadedImageHeight = mImage.Height;
                }
                if (didAdjust)
                {
                    await mImage.WriteAsync(convertedOutputPath);
                    filePath = convertedOutputPath;
                    LogInfo(string.Format("Saved adjusted image to JPEG; file path is now {0}", filePath));                    
                }
                // write to PDF
                var paragraph = section.AddParagraph();
                paragraph.Format.Alignment = ParagraphAlignment.Center;
                var image = paragraph.AddImage(filePath);
                image.LockAspectRatio = true;
                if (!isPDF && loadedImageHeight > 600)
                {
                    image.Height = 550; // make sure it will fit on one page
                }
                else
                {
                    image.Width = imageWidth; // can't be too wide now...not sure why...maybe due to margins...
                }
            }
            else
            {
                // need to render PDF to images
                if (_settings.UseDocnetPDFImageRendering)
                {
                    // render using Docnet library (which utilizes pdfium, the chrome renderer)
                    string RenderPdfPageToImage(IDocReader docReader, int pgNum)
                    {
                        Console.WriteLine("Rendering pg " + pgNum);
                        using var pageReader = docReader.GetPageReader(pgNum);
                        Console.WriteLine("Getting image for page " + pgNum);
                        var rawBytes = pageReader.GetImage(RenderFlags.RenderAnnotations);
                        Console.WriteLine("Getting width & height for page " + pgNum);
                        var width = pageReader.GetPageWidth();
                        var height = pageReader.GetPageHeight();
                        Console.WriteLine("Loading pixel data for page " + pgNum);
                        using var img = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
                        // you are likely going to want this as well otherwise you might end up with transparent parts.
                        img.Mutate(x => x.BackgroundColor(SixLabors.ImageSharp.Color.White));
                        var pdfPageImageOutputPath = Path.Combine(convertedDir, info.Name + "-Page-" 
                            + (pgNum + 1).ToString().PadLeft(3, '0') + ".jpg");
                        img.Save(pdfPageImageOutputPath);
                        Console.WriteLine("Done rendering pg " + pgNum);
                        return pdfPageImageOutputPath;
                    }
                    // render all pages to images
                    var docReader = DocLib.Instance.GetDocReader(
                        filePath,
                        new PageDimensions(1080, 1920)); // TODO: are these dims right?
                    // add to document
                    var pgCount = docReader.GetPageCount();
                    if (pgCount > 0)
                    {
                        var convertedPdfImagePath = RenderPdfPageToImage(docReader, 0);
                        imageTitlePar.AddText(string.Format(" (PDF with {0} page{1}) ", 
                            pgCount, 
                            pgCount == 1 ? "" : "s"));
                        var paragraph = section.AddParagraph();
                        paragraph.Format.Alignment = ParagraphAlignment.Center;
                        var image = paragraph.AddImage(convertedPdfImagePath);
                        image.Width = imageWidth;
                        image.LockAspectRatio = true;
                        for (var j = 1; j < pgCount; j++)
                        {
                            section.AddPageBreak();
                            paragraph = section.AddParagraph();
                            paragraph.Format.Alignment = ParagraphAlignment.Center;
                            convertedPdfImagePath = RenderPdfPageToImage(docReader, j);
                            image = paragraph.AddImage(convertedPdfImagePath);
                            image.LockAspectRatio = true;
                            image.Width = imageWidth;
                        }
                    }
                }
                else
                {
                    // render first page (eventually need to improve code to just do everything in a loop)
                    var paragraph = section.AddParagraph();
                    paragraph.Format.Alignment = ParagraphAlignment.Center;
                    var image = paragraph.AddImage(filePath);
                    image.LockAspectRatio = true;
                    image.Width = imageWidth; // can't be too wide now...not sure why...maybe due to margins...
                    // render other PDF pages, if any
                    // see: https://stackoverflow.com/a/65091204/3938401
                    var pdfFileToAdd = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
                    var pgCount = pdfFileToAdd.PageCount;
                    imageTitlePar.AddText(string.Format(" (PDF with {0} page{1}) ", 
                        pgCount, 
                        pgCount == 1 ? "" : "s"));
                    for (var j = 2; j <= pgCount; j++)
                    {
                        section.AddPageBreak();
                        paragraph = section.AddParagraph();
                        paragraph.Format.Alignment = ParagraphAlignment.Center;
                        image = paragraph.AddImage(filePath + "#" + j);
                        image.LockAspectRatio = true;
                        image.Width = imageWidth;
                    }
                }
            }
            LogInfo(string.Format("Added image: {0} ({1})", file.Title, filePath));
            hasAddedData = true;
        }
        var pdfRenderer = new PdfDocumentRenderer
        {
            Document = pdfDoc,
            WorkingDirectory = folderPath
        };
        LogInfo("Rendering document to PDF file...");
        pdfRenderer.RenderDocument();
        string outputPDFFilePath = Path.Join(outputDir, outputFileName);
        LogInfo("Saving PDF document to disk...");
        pdfRenderer.PdfDocument.Save(outputPDFFilePath);
        LogInfo("Finished saving PDF output to: " + outputPDFFilePath);
        await CreateAndSaveReportObjectAfterReportCreation();
        // clean up data dir
        Directory.Delete(convertedDir, true);
        // show output folder to user
        OpenFolderForFileInFileViewer(outputPDFFilePath);
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