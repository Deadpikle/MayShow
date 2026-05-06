using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


#if IOS
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
#endif 

namespace MayShow.Helpers;

class MobileUtilities
{
    #if IOS

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
        await sourceStream.CopyToAsync(localFileStream);
        return outputPath;
    }

    public static async Task<List<string>> PickPhotosAndSaveToDir(string saveDir)
    {
        var output = new List<string>();
        var fileResults = await PickPhotos();
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
                return await SaveFileResultToDir(photo, saveDir);
            }
        }
        return null;
    }

    public static async Task ShareFile(string pathToFile, string title)
    {
        // if we need to set the location of the popover:
        // https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/data/share?view=net-maui-10.0&tabs=macios#presentation-location
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(pathToFile)
        });
    }

    public static async Task PutTextOntoClipboard(string text)
    {
        await Clipboard.Default.SetTextAsync(text);
    }

    #endif
}