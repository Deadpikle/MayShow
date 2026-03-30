
using System.IO;
using MayShow.Interfaces;
using PdfSharp.Fonts;

namespace MayShow.Helpers;

class PDFFontResolver : IFontResolver
{
    private string _runningProcessDirectory;
    private ILogger? _logger;

    public PDFFontResolver(string runningProcessDirectory, ILogger? logger)
    {
        _logger = logger;
        _runningProcessDirectory = runningProcessDirectory;
    }

    public byte[]? GetFont(string faceName)
    {
        _logger?.LogInfo(string.Format("Loading font {0}", faceName));
        if (faceName == "Noto Sans")
        {
            var path = Path.Combine(_runningProcessDirectory, "Assets/Fonts/Noto_Sans/static/NotoSans-Regular.ttf");
            if (!File.Exists(path))
            {
                path = Path.Combine(_runningProcessDirectory, "../Resources/Assets/Fonts/Noto_Sans/static/NotoSans-Regular.ttf");
            }
            return File.ReadAllBytes(path);
        }
        if (faceName == "Noto Sans Bold")
        {
            var path = Path.Combine(_runningProcessDirectory, "Assets/Fonts/Noto_Sans/static/NotoSans-Bold.ttf");
            if (!File.Exists(path))
            {
                path = Path.Combine(_runningProcessDirectory, "../Resources/Assets/Fonts/Noto_Sans/static/NotoSans-Bold.ttf");
            }
            return File.ReadAllBytes(path);
        }
        if (faceName == "Noto Sans JP")
        {
            var path = Path.Combine(_runningProcessDirectory, "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf");
            if (!File.Exists(path))
            {
                path = Path.Combine(_runningProcessDirectory, "../Resources/Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf");
            }
            return File.ReadAllBytes(path);
        }
        if (faceName == "Noto Sans JP Bold")
        {
            var path = Path.Combine(_runningProcessDirectory, "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-SemiBold.ttf");
            if (!File.Exists(path))
            {
                path = Path.Combine(_runningProcessDirectory, "../Resources/Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-SemiBold.ttf");
            }
            return File.ReadAllBytes(path);
        }
        return null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        // LogInfo(string.Format("Resolving font name {0}", familyName));
        if (familyName == "Noto Sans")
        {
            if (bold)
            {
                return new FontResolverInfo(familyName + " Bold");
            }
            return new FontResolverInfo(familyName);
        }
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
}