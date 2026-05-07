using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;


#if IOS
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
#endif 

namespace MayShow.Helpers;

class MobileUtilities
{
    #if IOS

    public static string GetDataDirBasePath()
    {
        return FileSystem.Current.AppDataDirectory;
    }

    private static async Task<List<FileResult>> PickPhotos()
    {
        var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
        {
            // Default is 1; set to 0 for no limit
            SelectionLimit = 0,
            // Optional processing for images
            // MaximumWidth = 1024,
            // MaximumHeight = 768,
            CompressionQuality = 90,
            RotateImage = true,
            PreserveMetaData = false,
        });

        return results;
    }

    private static async Task<string> SaveFileResultToDir(FileResult result, string saveDir)
    {
        // note: docs say the following --
        //      The FullPath property doesn't always return the physical path to the file. 
        //      To get the file, use the OpenReadAsync method.
        // So, make sure to always use OpenReadAsync() instead.
        var outputPath = Path.Combine(saveDir, Guid.NewGuid().ToString() + Path.GetExtension(result.FileName));
        using Stream sourceStream = await result.OpenReadAsync();
        using FileStream localFileStream = File.OpenWrite(outputPath);
        Console.WriteLine("Writing file result to {0}", outputPath);
        await sourceStream.CopyToAsync(localFileStream);
        return outputPath;
    }

    public static async Task<List<string>> PickPhotosAndSaveToDir(string saveDir)
    {
        var output = new List<string>();
        var fileResults = await PickPhotos();
        Console.WriteLine("User picked {0} photos", fileResults.Count);
        if (fileResults.Count > 0 && !Directory.Exists(saveDir))
        {
            Console.WriteLine("Made save directory when picking photos");
            Directory.CreateDirectory(saveDir);
        }
        foreach (var fileResult in fileResults)
        {
            output.Add(await SaveFileResultToDir(fileResult, saveDir));
        }
        return output;
    }

    public static bool CanTakePhotos()
    {
        return MediaPicker.Default.IsCaptureSupported;
    }

    public static async Task<string?> TakePhoto(string saveDir)
    {
        if (CanTakePhotos())
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }
                return await SaveFileResultToDir(photo, saveDir);
            }
        }
        return null;
    }

    public static async Task ShareFile(string pathToFile, string title)
    {
        // write it to cache dir per documentation
        // https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/share?view=net-maui-10.0&tabs=macios
        var cachedDestFilePath = Path.Combine(FileSystem.Current.CacheDirectory, Path.GetFileName(pathToFile));
        File.Copy(pathToFile, cachedDestFilePath, true);
        // if we need to set the location of the popover:
        // https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/share?view=net-maui-10.0&tabs=macios#presentation-location
        _ = Dispatcher.UIThread.Invoke(async () =>
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(cachedDestFilePath)
            });
        });
    }

    public static async Task PutTextOntoClipboard(string text)
    {
        await Clipboard.Default.SetTextAsync(text);
    }

    public static (uint, uint) GetImageWidthHeight(string path)
    {
        using FileStream openImageFileStream = File.OpenRead(path);
        var im = PlatformImage.FromStream(openImageFileStream);
        return ((uint)im.Width, (uint)im.Height);
    }

    #endif
}