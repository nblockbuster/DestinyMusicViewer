using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DestinyMusicViewer
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void DMVLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"License.txt\"";
            fileopener.Start();
        }

        private void NAudioLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"NAudioLicense.txt\"";
            fileopener.Start();
        }

        private void wpftoolkitLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"wpftoolkitlicense.txt\"";
            fileopener.Start();
        }

        private void NAudioVorbisLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"NAudioVorbisLicense.txt\"";
            fileopener.Start();
        }

        private void WwiseParserLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"parserlicense.txt\"";
            fileopener.Start();
        }

        private void NewtonsoftLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"newtonsoftlicense.txt\"";
            fileopener.Start();
        }
    }
}
