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

namespace MayShow.ViewModels;

class AboutViewModel
{
    public AboutViewModel()
    {
    }

    public void Close()
    {
        DialogHost.Close("DialogHost", null);
    }
}