using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MayShow.Views
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            this.InitializeComponent();

            // set license text
            var licenseFileName = "Assets/LICENSES.txt";
            var licenseText = "";
            if (File.Exists(licenseFileName))
            {
                licenseText = File.ReadAllText(licenseFileName);
            }
            else
            {
                licenseFileName = "../Resources/Assets/LICENSES.txt";
                if (File.Exists(licenseFileName))
                {
                    licenseText = File.ReadAllText(licenseFileName);
                }
                else
                {
                    licenseText = "Error: Unable to find license file!";
                }
            }
            LicenseTextBlock.Text = licenseText;
        }
    }
}
