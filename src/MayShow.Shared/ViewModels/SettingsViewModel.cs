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
using System.Collections.Generic;

namespace MayShow.ViewModels;

class SettingsViewModel: ChangeNotifier
{
    private Settings _previousSettings;
    private Settings _settings;
    private string _errorMessage;
    private ITopLevelGrabber? _topLevelGrabber;
    private List<DateDisplayFormat> _dateFormats;
    private int _gridDisplayDateFormatSelectedIndex;
    private int _reportDisplayDateFormatSelectedIndex;

    public SettingsViewModel(Settings settingsToEdit, ITopLevelGrabber? topLevelGrabber): base()
    {
        _previousSettings = settingsToEdit;
        _settings = new Settings(settingsToEdit); // clone it
        _errorMessage = "";
        _topLevelGrabber = topLevelGrabber;
        _dateFormats = Constants.GetDateDisplayFormats();
        _gridDisplayDateFormatSelectedIndex = _dateFormats.FindIndex(x => x.Value == _previousSettings.DataGridDateFormat);
        if (_gridDisplayDateFormatSelectedIndex == -1)
        {
            _gridDisplayDateFormatSelectedIndex = 0;
        }
        _reportDisplayDateFormatSelectedIndex = _dateFormats.FindIndex(x => x.Value == _previousSettings.ReportDateFormat);
        if (_reportDisplayDateFormatSelectedIndex == -1)
        {
            _reportDisplayDateFormatSelectedIndex = 0;
        }
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
            NotifyPropertyChanged(nameof(IsOutputPdfDirValid));
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
            NotifyPropertyChanged();
        }
    }

    public bool SaveReportJsonDataInInternalDir
    {
        get => _settings.SaveReportJsonDataInInternalDir;
        set
        {
            _settings.SaveReportJsonDataInInternalDir = value;
            NotifyPropertyChanged();
        }
    }

    public List<DateDisplayFormat> DateFormats
    {
        get => _dateFormats;
    }

    public int DataGridDisplayDateFormatSelectedIndex
    {
        get => _gridDisplayDateFormatSelectedIndex;
        set
        {
            _gridDisplayDateFormatSelectedIndex = value;
            _settings.DataGridDateFormat = _dateFormats[value].Value;
            NotifyPropertyChanged();
        }
    }

    public int ReportDisplayDateFormatSelectedIndex
    {
        get => _reportDisplayDateFormatSelectedIndex;
        set
        {
            _reportDisplayDateFormatSelectedIndex = value;
            _settings.ReportDateFormat = _dateFormats[value].Value;
            NotifyPropertyChanged();
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

    public void OpenSettingsDir()
    {
        var topLevel = _topLevelGrabber?.GetTopLevel();
        var dirName = Utilities.GetInternalDataPath();
        if (topLevel is not null && dirName != null)
        {
            var launcher = topLevel.Launcher;
            launcher.LaunchUriAsync(new Uri(dirName));
        }
    }

    public void Cancel()
    {
        DialogHost.Close("DialogHost", null);
    }

    public void Save()
    {
        if (!IsOutputPdfDirValid)
        {
            ErrorMessage = "Output directory cannot be found!";
        }
        else
        {
            ErrorMessage = "";
            DialogHost.Close("DialogHost", _settings);
        }
    }
}