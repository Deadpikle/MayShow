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
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
using PdfSharp.Snippets.Font;
using MayShow.Interfaces;
using MayShow.Models;

namespace MayShow.ViewModels;

class EditFileViewModel : BaseViewModel
{
    ReportFile _clonedFile;
    ReportFile _originalFile;
    
    public EditFileViewModel(ReportFile file, IChangeViewModel viewModelChanger) : base(viewModelChanger)
    {
        _clonedFile = new ReportFile(file);
        _originalFile = file;
    }

    public ReportFile OriginalFile
    {
        get => _originalFile;
    }

    public ReportFile ClonedFile
    {
        get => _clonedFile;
    }

    public void Cancel()
    {
        DialogHost.Close("DialogHost", null);
    }

    public void Save()
    {
        DialogHost.Close("DialogHost", ClonedFile);
    }
}