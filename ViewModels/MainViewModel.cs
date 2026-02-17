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
using ReceiptPDFBuilder.Helpers;
using ReceiptPDFBuilder.Interfaces;
using ReceiptPDFBuilder.Models;
using ReceiptPDFBuilders.Helpers;

namespace ReceiptPDFBuilder.ViewModels;

class MainViewModel : BaseViewModel, IFontResolver
{
    private string _processDir;
    private bool _isCreatingPDF;
    private string _createPDFLog;
    private string _workingFolder;

    private string _reportTitle;
    private ObservableCollection<ReportFile> _reportFiles;
    private DateTime? _lastGeneratedTime;

    private Settings _settings;

    public MainViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
    {
        _processDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
        _isCreatingPDF = false;
        var quotes = Constants.GetQuotes();
        Random random = new Random();
        var quoteIndex = random.Next(0, quotes.Length);
        _createPDFLog = "----- Receipt PDF Builder v1.0.0 ------" + Environment.NewLine;
        _createPDFLog += quotes[quoteIndex] + Environment.NewLine;
        _createPDFLog += "---------------------------------------" + Environment.NewLine;
        _createPDFLog += "Ready to create PDF!";
        _workingFolder = "";
        _reportFiles = new ObservableCollection<ReportFile>();
        _reportFiles.CollectionChanged += ( sender, e ) => { NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled)); };
        _reportTitle = "";
        _lastGeneratedTime = null;
        _settings = Settings.LoadSettings();
        if (!string.IsNullOrWhiteSpace(_settings.LastUsedPath))
        {
            LogInfo("Loading data at last used path of {0}", _settings.LastUsedPath);
            ScanFolder(_settings.LastUsedPath);
        }
    }

    public string ReportTitle
    {
        get => _reportTitle;
        set { _reportTitle = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsTitleBoxVisible)); }
    }

    public bool IsTitleBoxVisible
    {
        get => !string.IsNullOrWhiteSpace(_workingFolder);
    }

    public bool IsCreatingPDF
    {
        get => _isCreatingPDF;
        set { _isCreatingPDF = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsCreatePDFButtonEnabled)); }
    }

    public bool IsCreatePDFButtonEnabled
    {
        get => !_isCreatingPDF && _reportFiles.Count > 0;
    }

    public string CreatePDFLog
    {
        get => _createPDFLog;
        set { _createPDFLog = value; NotifyPropertyChanged(); }
    }

    public ObservableCollection<ReportFile> ReportFiles
    {
        get => _reportFiles;
        set { _reportFiles = value; NotifyPropertyChanged(); }
    }

    private void LogInfo(string message, params object[]? arguments)
    {
        var timestamp = string.Format("[{0:s}]", DateTime.Now);
        Console.WriteLine(timestamp + " " + message, arguments);
        CreatePDFLog += Environment.NewLine + string.Format(message, arguments ?? []);
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
            }
        }
    }

    private void ScanFolder(string path)
    {
        if (Directory.Exists(path))
        {
            _workingFolder = path;
            NotifyPropertyChanged(nameof(IsTitleBoxVisible));
            var reportFilePath = Path.Combine(path, GetReportSavedDataFileName());
            var successfullyLoadedPriorReport = false;
            if (File.Exists(reportFilePath))
            {
                // load prior report
                var json = File.ReadAllText(reportFilePath);
                var jsonContext = new SourceGenerationContext(Utilities.GetSerializerOptions());
                var report = JsonSerializer.Deserialize<PDFReport>(json, jsonContext.PDFReport);
                if (report != null && report.Files.Count > 0)
                {
                    ReportFiles = new ObservableCollection<ReportFile>(report.Files);
                    ReportTitle = report.Title;
                    _workingFolder = report.BaseFolder;
                    _lastGeneratedTime = report.LastGenerated ?? null;
                    LogInfo("Reloaded report last saved at {0}", report.LastSaved);
                    successfullyLoadedPriorReport = true;
                }
            }
            if (!successfullyLoadedPriorReport)
            {
                // Scan folder for files and display in DataGrid
                var filePaths = Directory.GetFiles(_workingFolder);
                foreach (var filePath in filePaths)
                {
                    AddFileBasedOnPath(filePath);
                }
                ResortPDFItemsByDate();
            }
        }
    }
    
    public void ShowAbout()
    {
        DialogHost.Show(new AboutViewModel());
    }

    public void RemoveFile(object f) => RemoveFileImpl((ReportFile)f);

    public async void RemoveFileImpl(ReportFile file)
    {
        var result = await DialogHost.Show(new WarningDeleteItemModel(file));
        if (result != null && (bool)result)
        {
            var idx = ReportFiles.IndexOf(file);
            if (idx != -1)
            {
                ReportFiles.RemoveAt(idx);
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
        }
    }

    private string[] GetAllowedFileExtensionPatterns()
    {
        return [ "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp", "*.pdf", "*.heic", ];
    }

    private string[] GetAllowedFileExtensionPatternsWithoutStar()
    {
        var list = GetAllowedFileExtensionPatterns();
        return list.Select(x => x.Replace("*.", "")).ToArray();
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
                        Patterns = GetAllowedFileExtensionPatterns(),
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
            var fileExtensions = GetAllowedFileExtensionPatternsWithoutStar();
            var didMatch = false;
            foreach (var fileExtension in fileExtensions)
            {
                if (filePath.EndsWith("." + fileExtension))
                {
                    didMatch = true;
                    break;
                }
            }
            if (!didMatch)
            {
                LogInfo("File {0} did not match allowed file extension types, so it was not added.", filePath);
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
            Console.WriteLine(file.FilePath);
            var launcher = topLevel.Launcher;
            launcher.LaunchUriAsync(new Uri(file.FilePath));
        }
    }

    public void OpenFileLocation(object f) => OpenFileLocationImpl((ReportFile)f);

    private void OpenFileLocationImpl(ReportFile file)
    {
        var topLevel = TopLevelGrabber?.GetTopLevel();
        var dirName = Path.GetDirectoryName(file.FilePath);
        if (topLevel is not null && dirName != null)
        {
            Console.WriteLine(file.FilePath);
            var launcher = topLevel.Launcher;
            launcher.LaunchUriAsync(new Uri(dirName));
        }
    }

    public void ResortPDFItemsByDate()
    {
        LogInfo("Sorting report files list...");
        ReportFiles = new ObservableCollection<ReportFile>(ReportFiles.OrderBy(x => x.ReceiptDateTime));
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
                await Task.Run(() => CreatePDF(_workingFolder));
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

    public async void SaveInterimReportInfo()
    {
        var report = new PDFReport()
        {
            Title = ReportTitle,
            Files = ReportFiles.ToList(),
            BaseFolder = _workingFolder,
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
        var savePath = Path.Combine(_workingFolder, GetReportSavedDataFileName());
        await File.WriteAllTextAsync(savePath, json);
        LogInfo("Saved report information to {0}", savePath);
    }

    private async Task CreateAndSaveReportObjectAfterReportCreation()
    {
        var report = new PDFReport()
        {
            Title = ReportTitle,
            Files = ReportFiles.ToList(),
            BaseFolder = _workingFolder,
            LastSaved = DateTime.Now,
            LastGenerated = DateTime.Now,
        };
        _lastGeneratedTime = DateTime.Now;
        await SavePDFReportDataToDisk(report);
    }

    private string GetReportSavedDataFileName()
    {
        return "report_data.json";
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
            return File.ReadAllBytes(Path.Combine(_processDir, "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-SemiBold.ttf"));
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
            imageTitlePar.AddText(file.Title);
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
            var isPDF = fileName.EndsWith(".pdf");
            // convert heic, webp, or png to JPEG for size and ease of use
            // (and probably compat reasons too, though I haven't tested that...)
            var lowerName = fileName.ToLower();
            var isHEIC = lowerName.EndsWith(".heic");
            var isWebp = lowerName.EndsWith(".webp");
            var isPNG = lowerName.EndsWith(".png");
            if (isHEIC || isWebp || isPNG)
            {
                var convertedDir = Path.Combine(folderPath, "converted");
                if (!Directory.Exists(convertedDir))
                {
                    Directory.CreateDirectory(convertedDir);
                }
                var info = new FileInfo(file.FilePath);
                using var mImage = new MagickImage(info.FullName);
                // Save frame as jpg
                var outputPath = Path.Combine(convertedDir, info.Name + ".jpg");
                mImage.Quality = 80;
                mImage.Scale((uint)Math.Floor(mImage.Width * 0.5), (uint)Math.Floor(mImage.Height * 0.5));
                await mImage.WriteAsync(outputPath);
                filePath = Path.Combine("Converted", info.Name + ".jpg");
                LogInfo(string.Format("Converted image to JPEG; fileName is now {0}", file.FilePath));
            }
            var paragraph = section.AddParagraph();
            paragraph.Format.Alignment = ParagraphAlignment.Center;
            var image = paragraph.AddImage(filePath);
            image.LockAspectRatio = true;
            image.Width = imageWidth; // can't be too wide now...not sure why...maybe due to margins...
            LogInfo(string.Format("Added image: {0} ({1})", file.Title, filePath));
            if (isPDF)
            {
                // add other PDF pages
                // see: https://stackoverflow.com/a/65091204/3938401
                var pdfFileToAdd = PdfReader.Open(filePath);
                imageTitlePar.AddText(string.Format(" (PDF with {0} page{1}) ", 
                    pdfFileToAdd.PageCount, 
                    pdfFileToAdd.PageCount == 1 ? "" : "s"));
                for (var j = 2; j <= pdfFileToAdd.PageCount; j++)
                {
                    section.AddPageBreak();
                    paragraph = section.AddParagraph();
                    paragraph.Format.Alignment = ParagraphAlignment.Center;
                    image = paragraph.AddImage(filePath + "#" + j);
                    image.LockAspectRatio = true;
                    image.Width = imageWidth;
                }
            }
            hasAddedData = true;
        }
        var pdfRenderer = new PdfDocumentRenderer
        {
            Document = pdfDoc,
            WorkingDirectory = folderPath
        };
        LogInfo("Rendering document...");
        pdfRenderer.RenderDocument();
        string outputPDFFileName = Path.Join(folderPath, outputFileName);
        LogInfo("Saving document to disk...");
        pdfRenderer.PdfDocument.Save(outputPDFFileName);
        LogInfo("Saved PDF output to: " + outputPDFFileName);
        await CreateAndSaveReportObjectAfterReportCreation();
        IsCreatingPDF = false;
    }
}