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

class SettingsViewModel: ChangeNotifier
{
    private Settings _previousSettings;
    private Settings _settings;
    private string _errorMessage;
    private ITopLevelGrabber? _topLevelGrabber;

    public SettingsViewModel(Settings settingsToEdit, ITopLevelGrabber? topLevelGrabber)
    {
        _previousSettings = settingsToEdit;
        _settings = new Settings(settingsToEdit); // clone it
        _errorMessage = "";
        _topLevelGrabber = topLevelGrabber;
    }

    public bool UseDocnetPDFImageRendering
    {
        get => _settings.UseDocnetPDFImageRendering;
        set
        {
            _settings.UseDocnetPDFImageRendering = value;
            NotifyPropertyChanged();
        }
    }

    public bool SaveOutputPdfInWorkingDir
    {
        get => _settings.SaveOutputPdfInWorkingDir;
        set
        {
            _settings.SaveOutputPdfInWorkingDir = value;
            NotifyPropertyChanged();
        }
    }

    public string OutputPdfDirPath
    {
        get => _settings.OutputPdfDir;
        set
        {
            _settings.OutputPdfDir = value;
            NotifyPropertyChanged();
        }
    }

    public bool IsOutputPdfDirValid
    {
        get => SaveOutputPdfInWorkingDir || (!SaveOutputPdfInWorkingDir && Directory.Exists(OutputPdfDirPath));
    }

    public bool HasErrorMessage
    {
        get => !string.IsNullOrWhiteSpace(_errorMessage);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HasErrorMessage));
        }
    }

    public decimal ImageResizeThreshold
    {
        get => _settings.ImageResizeThreshold;
        set
        {
            _settings.ImageResizeThreshold = value;
            NotifyPropertyChanged(nameof(ImageResizeThreshold));
        }
    }

    public async void ChooseOutputFolder()
    {
        var topLevel = _topLevelGrabber?.GetTopLevel();
        if (topLevel != null)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = "Choose where to save your report file...",
                AllowMultiple = false,
            });
            if (folders.Count == 1)
            {
                var folder = folders[0];
                OutputPdfDirPath = folder.Path.LocalPath;
            }
        }
    }

    public void Cancel()
    {
        DialogHost.Close("DialogHost", null);
    }

    public void Save()
    {
        DialogHost.Close("DialogHost", _settings);
    }
}