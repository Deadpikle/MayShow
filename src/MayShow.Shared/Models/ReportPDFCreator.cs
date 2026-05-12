using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DialogHostAvalonia;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using MayShow.Helpers;
using MayShow.Interfaces;
using MayShow.ViewModels;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
using PdfSharp.Snippets.Font;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

#if !IOS
using ImageMagick;
#endif

#if IOS
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
#endif

namespace MayShow.Models;

class ReportPDFCreator : ChangeNotifier
{
    private ILogger _logger;

    public ReportPDFCreator(ILogger logger)
    {
        _logger = logger;
    }

    private Paragraph GetFooterParagraph()
    {
        var footerPar = new Paragraph();
        footerPar.Format.Alignment = ParagraphAlignment.Center;
        footerPar.Format.Font.Size = 10;
        footerPar.AddText("--Page ");
        footerPar.AddPageField();
        footerPar.AddText(" of ");
        footerPar.AddNumPagesField();
        footerPar.AddText("--");
        footerPar.AddLineBreak();
        footerPar.AddText("Report generated on " + DateTime.Now.ToString("f") + " with MayShow v" + Constants.AppVersion);
        footerPar.Tag = "FooterPar";
        footerPar.Format.Font.Name = "Noto Sans";
        return footerPar;
    }

    private decimal GetExistingPageItemHeight(PdfDocumentRenderer pdfRenderer, decimal footerParagraphHeight)
    {
        pdfRenderer.DocumentRenderer.PrepareDocument();
        var currPageCount = pdfRenderer.DocumentRenderer.FormattedDocument?.PageCount;
        var heightForExistingItemsOnPage = footerParagraphHeight;
        if (currPageCount.HasValue)
        {
            var renderInfo = pdfRenderer.DocumentRenderer.GetRenderInfoFromPage(currPageCount.Value);
            if (renderInfo != null)
            {
                // Console.WriteLine("Got render info for page: {0}", currPageCount);
                foreach (var item in renderInfo)
                {
                    heightForExistingItemsOnPage += (decimal)item.LayoutInfo.ContentArea.Height.Inch;
                }
            }
        }
        return heightForExistingItemsOnPage;
    }

    private Paragraph MakeParagraph(Section section, string text, bool isBold, int fontSize, string tag, bool isCenter = true)
    {
        const string defaultFontName = "Noto Sans JP";
        var par = section.AddParagraph();
        par.Format.Alignment = isCenter ? ParagraphAlignment.Center : ParagraphAlignment.Left;
        par.Format.Font.Size = fontSize;
        par.Format.Font.Bold = isBold;
        par.Format.Font.Name = defaultFontName; // has english letters in it, too
        par.AddText(text);
        par.Tag = tag;
        return par;
    }

    #if !IOS
    private string RenderPdfPageToImage(IDocReader docReader, int pgNum, string convertedDir, string fileName)
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
        var pdfPageImageOutputPath = Path.Combine(convertedDir, fileName + "-Page-" 
            + (pgNum + 1).ToString().PadLeft(3, '0') + ".jpg");
        img.Save(pdfPageImageOutputPath);
        Console.WriteLine("Done rendering pg " + pgNum);
        return pdfPageImageOutputPath;
    }
    #endif

    #if IOS
    private string? RenderPdfPageToImage(string inputFilePathAndName, int pageNum, string convertedDir, string fileName)
    {
        var pdfPageImageOutputPath = Path.Combine(convertedDir, fileName + "-Page-" 
            + (pageNum + 1).ToString().PadLeft(3, '0') + ".jpg");
        var result = iOS.Binding.ConvertPdfPageToImage.ExportPdfPageToImage(inputFilePathAndName, Path.Combine(convertedDir, pdfPageImageOutputPath), pageNum);
        if (result > 0)
        {
            return pdfPageImageOutputPath;
        }
        _logger?.LogInfo("Error result of creating PDF image for {0}: {1}", inputFilePathAndName, result);
        return null;
    }
    #endif

    // https://forum.pdfsharp.net/viewtopic.php?f=2&t=1025
    public async Task<string?> CreatePDF(PDFReport reportData, string reportTitle, string outputFilePathWithName, PDFFontResolver fontResolver, Settings appSettings)
    {
        // setup globals and consts...
        GlobalFontSettings.FontResolver = fontResolver;
        GlobalFontSettings.FallbackFontResolver = new FailsafeFontResolver();
        const int maxImageWidth = 425;
        const decimal pageWidth = 8.5m;
        const decimal pageHeight = 11.0m;
        const decimal margin = 0.5m;
        const int imageResolution = 72;
        const int imageInsertMarginPixels = 30; // we calculate max available; use max - this # for max image size
        const decimal reduceImageSizeAmount = 0.95m;
        var maxItemPxWidth = ((pageWidth - (2 * margin)) * imageResolution) - imageInsertMarginPixels;
        var imageLineFormat = new MigraDoc.DocumentObjectModel.Shapes.LineFormat()
        {
            Color = MigraDoc.DocumentObjectModel.Colors.Black,
            Width = Unit.FromPoint(2),
        };;
        // start making PDF!
        var convertedDir = Utilities.GetTempConvertedImagesFolderPath();
        // create doc and setup initial section (for page characteristics)
        var pdfDoc = new Document();
        var section = pdfDoc.AddSection();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.PageWidth = pageWidth + "in";
        section.PageSetup.PageHeight = pageHeight + "in";
        section.PageSetup.TopMargin = margin + "in";
        section.PageSetup.RightMargin = margin + "in";
        section.PageSetup.BottomMargin = margin + "in";
        section.PageSetup.LeftMargin = margin + "in";
        // setup footer for page number
        var footerPar = GetFooterParagraph();
        section.Footers.Primary.Add(footerPar);
        // create a quick PDF doc renderer to measure footer paragraph height
        var footerParagraphHeight = 0.4m; // estimate
        var footerOnlyPdfDoc = new Document();
        var sectionClone = section.Clone();
        footerOnlyPdfDoc.Add(sectionClone);
        sectionClone.Add(GetFooterParagraph());
        var footerPdfRenderer = new PdfDocumentRenderer
        {
            Document = footerOnlyPdfDoc
        };
        footerPdfRenderer.DocumentRenderer.PrepareDocument();
        var footerRenderInfo = footerPdfRenderer.DocumentRenderer.GetRenderInfoFromPage(1);
        if (footerRenderInfo != null)
        {
            foreach (var item in footerRenderInfo)
            {
                if (item.DocumentObject.Tag?.ToString() == "FooterPar")
                {
                    Console.WriteLine("Got footer paragraph height!");
                    footerParagraphHeight = (decimal)item.LayoutInfo.ContentArea.Height.Inch;
                    break;
                }
            }
        }
        // continue setting up document
        // First page only: add report title
        MakeParagraph(section, reportTitle, true, 16, "TitlePar");
        //
        var outputFilePathNoName = Path.GetDirectoryName(outputFilePathWithName) ?? Utilities.GetTmpDataPath(); 
        var outputFileName = Path.GetFileName(outputFilePathWithName);
        if (string.IsNullOrWhiteSpace(outputFileName))
        {
            outputFileName = reportTitle + ".pdf";
        }
        var pdfRenderer = new PdfDocumentRenderer
        {
            Document = pdfDoc,
            WorkingDirectory = outputFilePathNoName
        };
        var hasAddedData = false;
        var internalDir = Utilities.GetInternalDataPath();
        for (var i = 0; i < reportData.Files.Count; i++)
        {
            var file = reportData.Files[i];
            var fileName = file.FileName;
            var filePath = file.FilePath;
            #if IOS
            // file.FilePath on iOS is just the file name, so get the full path to the file
            filePath = Path.Combine(reportData.BaseFolder, filePath);
            Console.WriteLine("On iOS, set image file path to {0}", filePath);
            #endif
            if (!File.Exists(filePath))
            {
                _logger?.LogInfo("ERROR: File \"{0}\" does not exist at path \"{1}\". Please remove it from the report or re-add it using the Add Item button if you still want it to be in this report.", file.Title, filePath);
                return null;
            }
            if (fileName == ".DS_Store" || fileName == outputFileName)
            {
                continue;
            }
            if (i > 0 && hasAddedData)
            {
                section.AddPageBreak();
            }
            var imageTitle = string.IsNullOrWhiteSpace(file.Title) ? file.FileName : file.Title;
            var imageTitlePar = MakeParagraph(section, imageTitle, true, 12, "ReceiptTitlePar");
            MakeParagraph(section, file.ReceiptDate.ToString(appSettings.ReportDateFormat), true, 12, "ReceiptDatePar");
            if (!string.IsNullOrWhiteSpace(file.Notes))
            {
                var imageNotesPar = MakeParagraph(section, file.Notes, false, 10, "ReceiptNotesPar");
            }
            var emptyPar = section.AddParagraph(); // add empty line for spacing
            emptyPar.Tag = "EmptyParagraph";
            // now add the image
            var lowerName = fileName.ToLower();
            var isPDF = lowerName.EndsWith(".pdf");
            // convert heic, webp, or png to JPEG for size and ease of use
            // (and probably compat reasons too, though I haven't tested that...)
            var isHEIC = lowerName.EndsWith(".heic");
            var isWebp = lowerName.EndsWith(".webp");
            var isPNG = lowerName.EndsWith(".png");
            var info = new FileInfo(filePath);
            uint loadedImageWidth = 0;
            uint loadedImageHeight = 0;
            // get max pixel height remaining for items on this page
            // (For multi-page PDFs, showing page 2 and on will have more height since they have no title, 
            // but to keep things consistent we will use the same height for all PDF pages.)
            // render up to now on this page and get height remaining in inches
            var currPageCount = pdfRenderer.DocumentRenderer.FormattedDocument?.PageCount;
            var heightForExistingItemsOnPage = GetExistingPageItemHeight(pdfRenderer, footerParagraphHeight);
            var remainingHeightInches = pageHeight - (2 * margin) - heightForExistingItemsOnPage;
            var remainingHeightPixels = (remainingHeightInches * imageResolution) - imageInsertMarginPixels;
            if (!isPDF)
            {
                var convertedOutputPath = Path.Combine(convertedDir, info.Name + ".jpg");
                #if IOS
                using FileStream openImageFileStream = File.OpenRead(filePath);
                var im = PlatformImage.FromStream(openImageFileStream);
                loadedImageWidth = (uint)im.Width;
                loadedImageHeight = (uint)im.Height;
                if (info.Length > appSettings.ImageResizeThreshold * 1024 * 1024)
                {
                    if (im.Width >= 400 || im.Height >= 400)
                    {
                        loadedImageWidth = (uint)Math.Floor(im.Width * 0.5);
                        loadedImageHeight = (uint)Math.Floor(im.Height * 0.5);
                        var resized = im.Resize(loadedImageWidth, loadedImageHeight, Microsoft.Maui.Graphics.ResizeMode.Stretch, true);
                        _logger?.LogInfo("Image {2} scaled to {0}x{1}", loadedImageWidth, loadedImageHeight, fileName);

                        using FileStream localFileStream = File.OpenWrite(convertedOutputPath);
                        await resized.SaveAsync(localFileStream, ImageFormat.Jpeg, 0.80f);
                        filePath = convertedOutputPath;
                        _logger?.LogInfo(string.Format("Saved adjusted image to JPEG; file path is now {0}", filePath));
                    }
                }
                #else
                using var mImage = new MagickImage(info.FullName);
                var didAdjust = false;
                _logger?.LogInfo("Image orientation of {0} is {1}", fileName, mImage.Orientation);
                if (mImage.Orientation != OrientationType.TopLeft)
                {
                    _logger?.LogInfo("Auto-adjusted image orientation of {0}", fileName);
                    mImage.AutoOrient();
                    didAdjust = true;
                }
                loadedImageWidth = mImage.Width;
                loadedImageHeight = mImage.Height;
                // perform needed image manipulations
                if (isHEIC || isWebp || isPNG || (info.Length > appSettings.ImageResizeThreshold * 1024 * 1024))
                {
                    // Save image as jpg
                    mImage.Quality = 80;
                    if (mImage.Width >= 400 || mImage.Height >= 400)
                    {
                        loadedImageWidth = (uint)Math.Floor(mImage.Width * 0.5);
                        loadedImageHeight = (uint)Math.Floor(mImage.Height * 0.5);
                        mImage.Scale(loadedImageWidth, loadedImageHeight);
                        _logger?.LogInfo("Image {2} scaled to {0}x{1}", loadedImageWidth, loadedImageHeight, fileName);
                    }
                    didAdjust = true;
                    _logger?.LogInfo("Converted image {0} to JPEG", fileName);
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
                    _logger?.LogInfo(string.Format("Saved adjusted image to JPEG; file path is now {0}", filePath));
                }
                #endif
                // write to PDF
                var paragraph = section.AddParagraph();
                paragraph.Format.Alignment = ParagraphAlignment.Center;
                var image = paragraph.AddImage(filePath);
                image.Resolution = imageResolution; // dots per inch
                image.Tag = "ReceiptImageTag";
                paragraph.Tag = "ReceiptImageParagraphTag";
                image.LineFormat = imageLineFormat.Clone();
                // resize down until it will fit on the page
                while (loadedImageHeight > remainingHeightPixels || loadedImageWidth > maxItemPxWidth)
                {
                    // Console.WriteLine("Image height = {0}, width = {1}; decreasing size by 5% to h={2}, w={3}", loadedImageHeight, loadedImageWidth, (uint)Math.Floor(loadedImageHeight * reduceImageSizeAmount), (uint)Math.Floor(loadedImageWidth * reduceImageSizeAmount));
                    // keep reducing size by 5% (little by little) until it fits on the page
                    // ...might skew ever so slightly but should not be noticable...
                    loadedImageHeight = (uint)Math.Floor(loadedImageHeight * reduceImageSizeAmount);
                    loadedImageWidth = (uint)Math.Floor(loadedImageWidth * reduceImageSizeAmount);
                }
                image.Height = loadedImageHeight;
                image.Width = loadedImageWidth;
            }
            else // isPDF
            {
                // need to render PDF to images
                if (appSettings.UseDocnetPDFImageRendering /* or #if IOS */)
                {
                    // render all pages to images using Docnet library (which utilizes pdfium, the chrome renderer)
                    #if IOS
                    var pgCount = iOS.Binding.GetPdfPageCount(filePath);
                    #else
                    var docReader = DocLib.Instance.GetDocReader(
                        filePath,
                        new PageDimensions(1080, 1920)); // TODO: are these dims right?
                    // add to document
                    var pgCount = docReader.GetPageCount();
                    #endif
                    if (pgCount > 0)
                    {
                        #if IOS
                        var convertedPdfImagePath = RenderPdfPageToImage(filePath, 0, convertedDir, info.Name);
                        if (convertedPdfImagePath == null)
                        {
                            _logger?.LogInfo("Unable to create image from PDF for {0}", filePath);
                            return null;
                        }
                        (var pdfPageImageWidth, var pdfPageImageHeight) = MobileUtilities.GetImageWidthHeight(convertedPdfImagePath);
                        #else
                        var convertedPdfImagePath = RenderPdfPageToImage(docReader, 0, convertedDir, info.Name);
                        using var firstPdfPageImage = new MagickImage(convertedPdfImagePath);
                        var pdfPageImageWidth = firstPdfPageImage.Width;
                        var pdfPageImageHeight = firstPdfPageImage.Height;
                        #endif
                        // get image height/width off of disk so we can resize down if needed;
                        // resize down until it will fit on the page
                        while (pdfPageImageHeight > remainingHeightPixels || pdfPageImageWidth > maxItemPxWidth)
                        {
                            pdfPageImageHeight = (uint)Math.Floor(pdfPageImageHeight * reduceImageSizeAmount);
                            pdfPageImageWidth = (uint)Math.Floor(pdfPageImageWidth * reduceImageSizeAmount);
                        }
                        imageTitlePar.AddText(string.Format(" (PDF with {0} page{1}) ", 
                            pgCount, 
                            pgCount == 1 ? "" : "s"));
                        var paragraph = section.AddParagraph();
                        paragraph.Format.Alignment = ParagraphAlignment.Center;
                        var image = paragraph.AddImage(convertedPdfImagePath);
                        image.LockAspectRatio = true;
                        image.LineFormat = imageLineFormat.Clone();
                        image.Height = pdfPageImageHeight;
                        image.Width = pdfPageImageWidth;
                        for (var j = 1; j < pgCount; j++)
                        {
                            section.AddPageBreak();
                            paragraph = section.AddParagraph();
                            paragraph.Format.Alignment = ParagraphAlignment.Center;
                            #if IOS
                            convertedPdfImagePath = RenderPdfPageToImage(filePath, 0, convertedDir, info.Name);
                            if (convertedPdfImagePath == null)
                            {
                                _logger?.LogInfo("Unable to create image from PDF for {0}", filePath);
                                return null;
                            }
                            (var otherPdfPageImageWidth, var otherPdfPageImageHeight) = MobileUtilities.GetImageWidthHeight(convertedPdfImagePath);
                            #else
                            convertedPdfImagePath = RenderPdfPageToImage(docReader, j, convertedDir, info.Name);
                            using var otherPdfPageImage = new MagickImage(convertedPdfImagePath);
                            var otherPdfPageImageWidth = otherPdfPageImage.Width;
                            var otherPdfPageImageHeight = otherPdfPageImage.Height;
                            #endif
                            // resize down until it will fit on the page
                            while (otherPdfPageImageHeight > remainingHeightPixels || pdfPageImageWidth > maxItemPxWidth)
                            {
                                pdfPageImageHeight = (uint)Math.Floor(otherPdfPageImageHeight * reduceImageSizeAmount);
                                otherPdfPageImageWidth = (uint)Math.Floor(otherPdfPageImageWidth * reduceImageSizeAmount);
                            }
                            image = paragraph.AddImage(convertedPdfImagePath);
                            image.LockAspectRatio = true;
                            image.Width = maxImageWidth;
                            image.LineFormat = imageLineFormat.Clone();
                            image.Height = otherPdfPageImageHeight;
                            image.Width = otherPdfPageImageWidth;
                        }
                    }
                }
                else
                {
                    // use older, not-docnet rendering method.
                    // uses MigraDoc rendering. Does not work with PDF annotations, and since Migradoc
                    // doesn't let us know how big the image is, we can't do the image resizing, so
                    // we just do our best.
                    // render first page (eventually need to improve code to just do everything in a loop)
                    var paragraph = section.AddParagraph();
                    paragraph.Format.Alignment = ParagraphAlignment.Center;
                    var image = paragraph.AddImage(filePath);
                    image.LockAspectRatio = true;
                    image.Width = maxImageWidth; // can't be too wide now...not sure why...maybe due to margins...
                    image.LineFormat = imageLineFormat.Clone();
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
                        image.Width = maxImageWidth;
                        image.LineFormat = imageLineFormat.Clone();
                    }
                }
            }
            _logger?.LogInfo(string.Format("Added image: {0} ({1})", file.Title, filePath));
            hasAddedData = true;
        }
        _logger?.LogInfo("Rendering document to PDF file...");
        pdfRenderer.DocumentRenderer.PrepareDocument(); // needed if you make edits after first PrepareDocument() is called
        pdfRenderer.RenderDocument();
        // actually save to disk now
        _logger?.LogInfo("Saving PDF document to disk...");
        pdfRenderer.PdfDocument.Save(outputFilePathWithName);
        _logger?.LogInfo("Finished saving PDF output to: " + outputFilePathWithName);
        // clean up converted files data dir
        Directory.Delete(convertedDir, true);
        // return output path
        return outputFilePathWithName;
    }
}