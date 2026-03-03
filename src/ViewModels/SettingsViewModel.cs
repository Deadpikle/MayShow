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

    public SettingsViewModel(Settings settingsToEdit)
    {
        _previousSettings = settingsToEdit;
        _settings = new Settings
        {
            LastUsedPath = _previousSettings.LastUsedPath,
            UseDocnetPDFImageRendering = _previousSettings.UseDocnetPDFImageRendering
        };
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

    public void Cancel()
    {
        DialogHost.Close("DialogHost", null);
    }

    public void Save()
    {
        DialogHost.Close("DialogHost", _settings);
    }
}