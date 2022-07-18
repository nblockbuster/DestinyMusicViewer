using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Net;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Vorbis;
using Tiger;
using Tiger.Formats;
using WwiseParserLib.Structures.Chunks;
using WwiseParserLib.Structures.Objects.HIRC;
using WwiseParserLib.Structures.SoundBanks;
using WwiseParserLib;
using Newtonsoft.Json;

namespace DestinyMusicViewer
{
    /// <summary>
    /// Interaction logic for BankView.xaml
    /// </summary>
    public partial class BankView : UserControl
    {
        private MainWindow mainWindow = null;
        private static string packages_path = string.Empty;
        private Extractor extractor = null;
        private SortedDictionary<string, Package> packages = new SortedDictionary<string, Package>();
        private Package CurrentPkg = null;
        string SelectedSoundBankName = string.Empty;
        string SelectedPlaylistId = string.Empty;
        public Dictionary<string, List<string>> SerializedMusicHierarchyDict = new Dictionary<string, List<string>>();

        public BankView()
        {
            InitializeComponent();
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            InitialiseConfig();
        }

        private void InitialiseConfig()
        {
            if (packages.Count > 0)
            {
                return;
            }
            mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow.config.AppSettings.Settings["PackagesPath"] != null)
            {
                SelectPkgsDirectoryButton.Visibility = Visibility.Hidden;
                Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                packages_path = config.AppSettings.Settings["PackagesPath"].Value;
                if(mainWindow._extractor != null)
                {
                    extractor = mainWindow._extractor;
                }
                else
                {
                    extractor = new Extractor(packages_path, LoggerLevels.HighVerbouse);
                }
                //extractor = new Extractor(packages_path, LoggerLevels.HighVerbouse);
                LoadList();
                Dispatcher.Invoke(() => PrimaryList.Items.Clear());
                ShowList();
            }
            else
            {
                MessageBox.Show($"No package path found.");
            }
        }

        public void SelectPkgsDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = $"Select the packages folder";
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                bool success = SetPackagePath(dialog.SelectedPath);
                if (success)
                {
                    SelectPkgsDirectoryButton.Visibility = Visibility.Hidden;
                    Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                    packages_path = config.AppSettings.Settings["PackagesPath"].Value;
                    extractor = new Extractor(packages_path, LoggerLevels.HighVerbouse);
                    LoadList();
                    Dispatcher.Invoke(() => PrimaryList.Items.Clear());
                    ShowList();
                }
            }
        }

        private bool SetPackagePath(string Path)
        {
            if (Path == "")
            {
                MessageBox.Show("Directory selected is invalid, please select the correct packages directory.");
                return false;
            }
            string[] files = Directory.GetFiles(Path, "*.pkg", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                MessageBox.Show("Directory selected is invalid, please select the correct packages directory.");
                return false;
            }
            if (!files[0].Contains("w64_"))
            {
                MessageBox.Show("Directory selected is invalid (not PC packages), please select the correct packages directory.");
                return false;
            }

            Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
            config.AppSettings.Settings.Remove("PackagesPath");
            config.AppSettings.Settings.Add("PackagesPath", Path);
            config.Save(ConfigurationSaveMode.Minimal);
            packages_path = Path;
            return true;
        }

        private void LoadList()
        {
            foreach (Package package in extractor.master_packages_stream())
            {
                if (CheckForBnks(package) && !package.no_patch_id_name.Contains("_en"))
                {
                    packages.Add(package.no_patch_id_name, package);
                }
            }
        }

        private void ShowList()
        {
            Dispatcher.Invoke(() => PrimaryList.Items.Clear());
            List<ToggleButton> ButtonList = new List<ToggleButton>();
            foreach (KeyValuePair<string, Package> package in packages)
            {
                ToggleButton btn = new ToggleButton();

                btn.Content = new TextBlock { Text = package.Key, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
                btn.Style = Application.Current.Resources["Button_Command"] as Style;
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.VerticalAlignment = VerticalAlignment.Center;
                btn.Background = new SolidColorBrush(Color.FromRgb(61, 61, 61));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                btn.Height = 75;
                btn.Width = 325;
                btn.Focusable = true;
                btn.Click += PackageButtonOnClick;

                ButtonList.Add(btn);
            }
            Dispatcher.Invoke(() => PrimaryList.ItemsSource = ButtonList);
        }

        private void PackageButtonOnClick(object sender, RoutedEventArgs e)
        {
            string ClickedPackageName = ((sender as ToggleButton).Content as TextBlock).Text;
            Package pkg = packages[ClickedPackageName];
            CurrentPkg = pkg;
            ShowBnkList(pkg);
            Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
        }

        private bool CheckForBnks(Package package)
        {
            foreach (Entry entry in package.entry_table())
            {
                if (entry.type == 26 && entry.subtype == 6)
                {
                    return true;
                }
            }
            return false;
        }

        private List<Entry> GetAllBnks(Package package)
        {
            List<Entry> BnkEntries = new List<Entry>();
            foreach (Entry entry in package.entry_table())
            {
                if (entry.type == 26 && entry.subtype == 6)
                {
                    BnkEntries.Add(entry);
                }
            }
            return BnkEntries;
        }

        private void ShowBnkList(Package pkg)
        {
            Dispatcher.Invoke(() => PrimaryList.ItemsSource = null);

            ToggleButton btn = new ToggleButton();
            btn.Style = Application.Current.Resources["Button_Command"] as Style;
            btn.HorizontalAlignment = HorizontalAlignment.Stretch;
            btn.VerticalAlignment = VerticalAlignment.Center;
            btn.Background = new SolidColorBrush(Color.FromRgb(61, 61, 61));
            btn.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
            btn.Height = 40;
            btn.Width = 325;
            btn.Focusable = false;
            btn.Content = "Go back to package list";
            btn.Padding = new Thickness(10, 5, 0, 5);
            btn.HorizontalContentAlignment = HorizontalAlignment.Left;
            btn.Click += GoBack_Click;

            Dispatcher.Invoke(() => PrimaryList.Items.Add(btn));

            foreach (Entry BnkEntry in GetAllBnks(pkg))
            {
                if (GetGinsorIds(BnkEntry).Distinct().Count() == 0)
                    continue;
                btn = new ToggleButton();
                btn.Focusable = true;

                btn.Content = new TextBlock
                {
                    Text = Utils.entry_name(pkg, BnkEntry) + " / " + BnkEntry.entry_a.ToHex().ToUpper() + "\nUses " + GetGinsorIds(BnkEntry).Distinct().Count() + " unique music files",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                };

                btn.Style = Application.Current.Resources["Button_Command"] as Style;
                btn.Height = 70;
                btn.Width = 325;
                btn.Background = new SolidColorBrush(Color.FromRgb(61, 61, 61));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                btn.HorizontalContentAlignment = HorizontalAlignment.Left;
                btn.Click += SoundBank_Click;

                Dispatcher.Invoke(() => PrimaryList.Items.Add(btn));
            }
        }

        private List<string> GetGinsorIds(Entry BnkEntry)
        {
            List<string> GinsorIds = new List<string>();

            byte[] BnkData = extractor.extract_entry_data(CurrentPkg, BnkEntry).data;
            Dictionary<string, List<uint>> IdToSegment = new Dictionary<string, List<uint>>();
            GinsorIds = mainWindow._dmv.genList(BnkData, ref IdToSegment);
            return GinsorIds;
        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            ShowList();
        }

        private void SoundBank_Click(object sender, RoutedEventArgs e)
        {
            SelectedSoundBankName = ((sender as ToggleButton).Content as TextBlock).Text.Split(" ")[0].Trim();
            SerializedMusicHierarchyDict = GenerateScript();
            //ShowScript(SerializedMusicHierarchy);
            ShowScript();
            Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
        }

        private Dictionary<string,List<string>> GenerateScript()
        {
            byte[] SoundBankData = extractor.extract_entry_data(Convert.ToUInt32(SelectedSoundBankName.Split("-")[0], 16), (int)Convert.ToUInt32(SelectedSoundBankName.Split("-")[1], 16)).data;

            SoundBank memSoundBank = new InMemorySoundBank(SoundBankData);
            var bkhd = memSoundBank.ParseChunk(SoundBankChunkType.BKHD);
            if (bkhd == null)
            {
                throw new Exception("The specified file does not have a valid SoundBank header.");
            }
            var hirc = memSoundBank.GetChunk(SoundBankChunkType.HIRC);
            if (hirc == null)
            {
                throw new Exception("The specified file does not have a valid Hierarchy header.");
            }

            return mainWindow._dmv.ParseBnkForPlaylists(memSoundBank);
        }

        private void ShowScript(List<string> SerializedMusicHierarchy)
        {
            Dispatcher.Invoke(() => HierarchyTextBlock.Text = "");
            foreach (var music_hier in SerializedMusicHierarchy)
            {
                Dispatcher.Invoke(() => HierarchyTextBlock.Text += music_hier + "\n");
            }
        }
        private void ShowScript()
        {
            List<ToggleButton> PlaylistButtons = new List<ToggleButton>();
            Dispatcher.Invoke(() => SecondaryList.ItemsSource = null);
            foreach (var dict_entry in SerializedMusicHierarchyDict)
            {
                //Dispatcher.Invoke(() => SecondaryList.ItemsSource = null);
                ToggleButton btn = new ToggleButton();
                btn.Style = Application.Current.Resources["Button_Command"] as Style;
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.VerticalAlignment = VerticalAlignment.Center;
                btn.Background = new SolidColorBrush(Color.FromRgb(61, 61, 61));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                btn.Height = 70;
                btn.Width = 325;
                btn.Focusable = false;
                btn.Content = new TextBlock
                {
                    Text = dict_entry.Key + "\n" + dict_entry.Value.Count + " music objects",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                };
                btn.Padding = new Thickness(10, 5, 0, 5);
                btn.HorizontalContentAlignment = HorizontalAlignment.Left;
                btn.Click += Playlist_Click;
                PlaylistButtons.Add(btn);
            }
            Dispatcher.Invoke(() => SecondaryList.ItemsSource = PlaylistButtons);
        }

        private void Playlist_Click(object sender, RoutedEventArgs e)
        {
            SelectedPlaylistId = ((sender as ToggleButton).Content as TextBlock).Text.Split("\n")[0];
            ShowScript(SerializedMusicHierarchyDict[SelectedPlaylistId]);
            Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
            Dispatcher.Invoke(() => HierarchyScroller.ScrollToTop());
        }
    }
}
