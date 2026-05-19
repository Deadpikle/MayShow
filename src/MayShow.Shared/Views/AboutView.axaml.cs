using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MayShow.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        this.InitializeComponent();

        // set license text
        var processDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
        var licenseFileName = Path.Combine(processDir, "Assets", "LICENSES.txt");
        var licenseText = "";
        if (File.Exists(licenseFileName))
        {
            licenseText = File.ReadAllText(licenseFileName);
        }
        else
        {
            licenseFileName = Path.Combine(processDir, "../Resources/Assets/LICENSES.txt");
            if (File.Exists(licenseFileName))
            {
                licenseText = File.ReadAllText(licenseFileName);
            }
            else
            {
                licenseText = "Error: Unable to find license file!";
            }
        }
        LicenseTextBlock.Text = licenseText.Trim();
    }
}
