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
    /// Interaction logic for BnkView.xaml
    /// </summary>
    public partial class BnkView : UserControl
    {
        public struct GinsorIdEntry
        {
            public Utils.EntryReference reference;
            public List<uint> SegmentIDs;

        }

        private static string packages_path;
        private static Extractor extractor;
        public Dictionary<string, GinsorIdEntry> dictlist = new Dictionary<string, GinsorIdEntry>();
        public List<string> GinsorIDList = new List<string>();
        private MainWindow mainWindow = null;
        private static WaveOut waveOut = new WaveOut();
        string CurrentGinsorId;
        int SelectedWemIndex = 0;
        bool export = false;
        bool seg = false;
        bool bIsSearched = false;
        VorbisWaveReader vorbis;

        public void log(string message)
        {
            
            string time_string = DateTime.Now.ToString("dd/MMM/yyyy hh:mm:ss tt");
            Dispatcher.Invoke(() => logging_box.Text += $"[{time_string}]: {message}\n");
            if (logging_box.Text.Length > 4096)
            {
                Dispatcher.Invoke(() => logging_box.Text = $"[{time_string}]:Text box reached > 4096 characters. Cleared.\n");
            }
            Dispatcher.Invoke(() => logging_box_scroller.ScrollToBottom());
        }

        public BnkView()
        {
            InitializeComponent();
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow.PkgCacheName != "")
            {
                InitialiseConfig();
            }
            Dispatcher.Invoke(() => VolSlider.Value = 1.0);
        }

        private void InitialiseConfig()
        {
            if (mainWindow.config.AppSettings.Settings["PackagesPath"] != null)
            {
                SelectPkgsDirectoryButton.Visibility = Visibility.Hidden;
                Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                packages_path = config.AppSettings.Settings["PackagesPath"].Value;
                extractor = new Extractor(packages_path, LoggerLevels.HighVerbouse);
                Dispatcher.Invoke(() => log("Loading..."));
                LoadList();
                Dispatcher.Invoke(() => PrimaryList.Items.Clear());
                ShowList();
                Dispatcher.Invoke(() => log("All loaded."));
            }
            else
            {
                MessageBox.Show($"No package path found.");
            }
        }

        private void LoadList()
        {
            
            List<uint> PackageIDs = new List<uint>();
            if (File.Exists("GinsorID_ref_dict.json"))
            {
                Dispatcher.Invoke(() => log("Found GinsorID_ref_dict.json file."));
                long length = new FileInfo("GinsorID_ref_dict.json").Length;
                if (length == 0)
                {
                    Dispatcher.Invoke(() => log("GinsorID_ref_dict.json empty."));
                }
                dictlist = JsonConvert.DeserializeObject<Dictionary<string, GinsorIdEntry>>(File.ReadAllText("GinsorID_ref_dict.json"));
                Dispatcher.Invoke(() => SelectPkgsDirectoryButton.Visibility = Visibility.Hidden);
                if (GinsorIDList.Count == 0)
                {
                    foreach (var entry in dictlist)
                    {
                        GinsorIDList.Add(entry.Key);
                    }
                }
            }
        }

        public void ShowList()
        {
            foreach (string GinsorId in GinsorIDList)
            {
                ToggleButton btn = new ToggleButton();
                Style style = Application.Current.Resources["Button_Command"] as Style;

                btn.Style = style;
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.VerticalAlignment = VerticalAlignment.Center;
                btn.Content = new TextBlock
                {
                    Text = GinsorId + "\nIn Package " + dictlist[GinsorId].reference.package_id.ToString("X2") + "\nIn SegmentIDs: ",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                };
                btn.Background = new SolidColorBrush(Color.FromRgb(61, 61, 61));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                btn.Height = 75;
                btn.Width = 320;
                btn.Focusable = true;
                btn.Click += GinsButton_Click;
                foreach (uint segment_id in dictlist[GinsorId].SegmentIDs)
                {
                    (btn.Content as TextBlock).Text += segment_id.ToString("X8") + " ";
                }
                Dispatcher.Invoke(() => PrimaryList.Items.Add(btn));
            }
        }

        private void GinsButton_Click(object sender, RoutedEventArgs e)
        {
            string ClickedGinsorId = ((sender as ToggleButton).Content as TextBlock).Text;
            CurrentGinsorId = ClickedGinsorId;
            PlayAndViewScript(ClickedGinsorId, sender, e);
        }

        private void PlayAndViewScript(string GinsorId, object sender, RoutedEventArgs e)
        {
            if (!bIsSearched)
            {
                SelectedWemIndex = PrimaryList.Items.IndexOf(sender as ToggleButton);   
            }

            ClickPlay(GinsorId, sender, e);
            if (export)
            {
                Export_Clicked(sender, e);
            }
            if (bIsSearched)
            {
                Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
                bIsSearched = false;
            }
        }

        private void ClickPlay(string GinsorId, object sender, RoutedEventArgs e)
        {
            string gins_id = GinsorId.Split("\n")[0];

            foreach (ToggleButton button in PrimaryList.Items)
            {
                Dispatcher.Invoke(() => button.IsChecked = false);
            }
            if ((sender as ToggleButton).IsChecked == true)
                Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
            if ((sender as ToggleButton).IsChecked == false)
                Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = true);

            if (SecondaryList.Children.Count > 0)
            {
                Dispatcher.Invoke(() => SecondaryList.Children.Clear());
            }
            foreach (var segid in dictlist[gins_id].SegmentIDs)
            {
                string SegmentId = segid.ToHex().ToUpper();
                GinsorIdsInSegment.Clear();
                var results = dictlist.Where(x => x.Value.SegmentIDs.Contains(segid));
                foreach (var result in results)
                {
                    GinsorIdsInSegment.Add(result.Key);
                }
                ToggleButton btn = new ToggleButton();
                Style style = Application.Current.Resources["Button_Command"] as Style;

                btn.Style = style;
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.VerticalAlignment = VerticalAlignment.Center;
                btn.Focusable = true;
                btn.Height = 75;
                btn.Click += PlaySegmentAudio;
                btn.Content = new TextBlock
                {
                    Text = "Segment Id: " + SegmentId + "\nContains GinsorIds: ",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                };

                foreach (var InSegmentGinsorId in GinsorIdsInSegment)
                {
                    (btn.Content as TextBlock).Text += InSegmentGinsorId + " ";
                }
                btn.Background = new SolidColorBrush(Color.FromRgb(61, 61, 61));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                Dispatcher.Invoke(() => SecondaryList.Children.Add(btn));
            }

            byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dictlist[gins_id].reference, extractor).Parse().data;
            PlayOgg(ogg_data);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            waveOut.Stop();
            Dispatcher.Invoke(() => PlaybackProgressBar.Value = 0);
            (sender as ToggleButton).IsChecked = false;
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {   
            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
            }
            else if (waveOut.PlaybackState == PlaybackState.Paused)
            {
                waveOut.Play();
                Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
            }
        }
        
        private void PlayOgg(byte[] OggData)
        {
            vorbis = new VorbisWaveReader(new MemoryStream(OggData));
            double duration = vorbis.Length / vorbis.WaveFormat.AverageBytesPerSecond;
            TrackInfoTextBlock.Text = "Track Info:\n    Length: " + duration.ToString("0.00") + "s";
            try
            {
                waveOut.Dispose();
                waveOut = new WaveOut();
                waveOut.Init(vorbis);
                Dispatcher.Invoke(() => PlaybackProgressBar.Maximum = duration * 1000);
                Dispatcher.Invoke(() => PlaybackProgressBar.Value = 0);

                waveOut.Play();
                Thread thread = new Thread(new ThreadStart(Run));
                thread.Start();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => log($"Error playing audio: {ex.Message}"));
            }
        }

        private void Run()
        {
            while (true)
            {
                //Debug.WriteLine("PlaybackState: " + waveOut.PlaybackState);

                if (waveOut.PlaybackState == PlaybackState.Playing)
                {
                    double ms = (waveOut.GetPosition() * 1000) / waveOut.OutputWaveFormat.BitsPerSample / waveOut.OutputWaveFormat.Channels * 8 / waveOut.OutputWaveFormat.SampleRate;
                    Dispatcher.Invoke(() => PlaybackProgressBar.Value = ms);
                }
                Thread.Sleep(120);
            }
        }

        private void VolSliderUpdated(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            waveOut.Volume = (float)VolSlider.Value;
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
                    Dispatcher.Invoke(() => log("Loading..."));
                    LoadList();
                    Dispatcher.Invoke(() => PrimaryList.Items.Clear());
                    ShowList();
                    Dispatcher.Invoke(() => log("All loaded."));
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
        
        private void SegmentSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                seg = true;
                Search();
            }
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                Search();
            }
        }

        List<string> GinsorIdsInSegment = new List<string>();

        private void Search()
        {
            if (seg)
            {
                if (SecondaryList.Children.Count > 0)
                {
                    Dispatcher.Invoke(() => SecondaryList.Children.Clear());
                }
                if (SegmentSearchBox.Text == "") return;
                string SegmentId = SegmentSearchBox.Text.ToUpper();
                GinsorIdsInSegment.Clear();
                var results = dictlist.Where(x => x.Value.SegmentIDs.Contains(Convert.ToUInt32(SegmentId, 16)));
                foreach (var result in results)
                {
                    GinsorIdsInSegment.Add(result.Key);
                }
                ToggleButton btn = new ToggleButton();
                Style style = Application.Current.Resources["Button_Command"] as Style;

                btn.Style = style;
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.VerticalAlignment = VerticalAlignment.Center;
                btn.Focusable = true;
                btn.Height = 75;
                btn.Click += PlaySegmentAudio;
                btn.Content = new TextBlock
                {
                    Text = "Segment Id: " + SegmentId + "\nContains GinsorIds: ",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                };
                
                foreach (var InSegmentGinsorId in GinsorIdsInSegment)
                {
                    (btn.Content as TextBlock).Text += InSegmentGinsorId + " ";
                }
                btn.Background = new SolidColorBrush(Color.FromRgb(61, 61, 61));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                Dispatcher.Invoke(() => SecondaryList.Children.Add(btn));
            }
            else
            {

                if (SearchBox.Text == "") return;
                if (SecondaryList.Children.Count > 0)
                {
                    Dispatcher.Invoke(() => SecondaryList.Children.Clear());
                }
                string GinsorId = SearchBox.Text.ToUpper();
                foreach (var id in GinsorIDList)
                {
                    if (id.ToUpper().Contains(GinsorId.ToUpper()))
                    {
                        bIsSearched = true;
                        ToggleButton btn = new ToggleButton();
                        Style style = Application.Current.Resources["Button_Command"] as Style;

                        btn.Style = style;
                        btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                        btn.VerticalAlignment = VerticalAlignment.Center;
                        btn.Focusable = true;
                        btn.Click += GinsButton_Click;
                        btn.Height = 75;
                        btn.Content = new TextBlock
                        {
                            Text = id + "\nIn Package " + dictlist[id].reference.package_id.ToString("X2") + "\nIn SegmentIDs: ",
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 13
                        };
                        foreach (uint segment_id in dictlist[id].SegmentIDs)
                        {
                            (btn.Content as TextBlock).Text += segment_id.ToString("X8") + " ";
                        }
                        btn.Background = new SolidColorBrush(Color.FromRgb(61, 61, 61));
                        btn.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                        Dispatcher.Invoke(() => SecondaryList.Children.Add(btn));
                    }
                }
            }
        }

        public void Export_Clicked(object sender, RoutedEventArgs e)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
            if (mainWindow.OutputPath == string.Empty)
            {
                if (config.AppSettings.Settings["OutputPath"] != null)
                {
                    mainWindow.OutputPath = config.AppSettings.Settings["OutputPath"].Value;
                }
                else
                {
                    using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        dialog.Description = $"Select an output folder";
                        System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                        if (dialog.SelectedPath == "")
                        {
                            MessageBox.Show("Directory selected is invalid, please select the correct packages directory.");
                            return;
                        }
                        mainWindow.OutputPath = dialog.SelectedPath;
                        config.AppSettings.Settings.Add("OutputPath", mainWindow.OutputPath);
                        config.Save(ConfigurationSaveMode.Minimal);
                    }
                }
            }
            string gins_id = "";
            if (seg)
            {
                gins_id = SegmentSearchBox.Text.ToUpper();
            }
            else if (PrimaryList.Items[SelectedWemIndex] != null)
            {
                gins_id = ((PrimaryList.Items[SelectedWemIndex] as ToggleButton).Content as TextBlock).Text.Split("\n")[0];
            }
            else if (SecondaryList.Children.Contains(sender as ToggleButton))
            {
                gins_id = ((SecondaryList.Children[SelectedWemIndex] as ToggleButton).Content as TextBlock).Text.Split("\n")[0];
                if (!GinsorIDList.Contains(gins_id.ToUpper()))
                {
                    return;
                }
            }
            else
            {
                gins_id = GinsorIDList[0];
                Dispatcher.Invoke(() => log("No valid GinsorID selected, using first GinsorID."));
            }
            string output_path = mainWindow.OutputPath + "\\" + gins_id;
            if (config.AppSettings.Settings["AudioFormat"] == null)
            {
                config.AppSettings.Settings.Add("AudioFormat", "Wem");
            }
            if (config.AppSettings.Settings["AudioFormat"].Value.ToString() == "Wem")
            {
                byte[] wem_data = extractor.extract_entry_data(dictlist[gins_id].reference).data;
                using (var writer = new FileStream(output_path + ".wem", FileMode.Create))
                {
                    writer.Write(wem_data, 0, wem_data.Length);
                }
            }
            else if (config.AppSettings.Settings["AudioFormat"].Value.ToString() == "Ogg")
            {
                byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dictlist[gins_id].reference, extractor).Parse().data;
                using (var writer = new FileStream(output_path + ".ogg", FileMode.Create))
                {
                    writer.Write(ogg_data, 0, ogg_data.Length);
                }
            }
            else if (config.AppSettings.Settings["AudioFormat"].Value.ToString() == "Wav")
            {
                byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dictlist[gins_id].reference, extractor).Parse().data;
                using (var vorbis = new VorbisWaveReader(new MemoryStream(ogg_data)))
                {
                    WaveFileWriter.CreateWaveFile(output_path + ".wav", vorbis);
                }
            }
            else
            {
                MessageBox.Show("Audio format not supported");
            }
            Dispatcher.Invoke(() => log($"Exported to {output_path}.{config.AppSettings.Settings["AudioFormat"].Value.ToLower()}"));
            if (((sender as ToggleButton).Content as TextBlock).Text.Contains("Selected"))
            {
                Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
            }
        }

        private void ExportWhenClickedOn_Clicked(object sender, RoutedEventArgs e)
        {
            if (export == true)
            {
                export = false;
                Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
            }
            else
            {
                export = true;
                Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = true);
            }
        }

        private void ExportScript_Click(object sender, RoutedEventArgs e)
        {
            (sender as ToggleButton).IsChecked = false;
            //TODO: Add logic to export a script for each playlist(?) a segment is in (get back to it later)
        }
  
        private void ExportAllButton_Click(object sender, RoutedEventArgs e)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
            if (mainWindow.OutputPath == string.Empty)
            {
                if (config.AppSettings.Settings["OutputPath"] != null)
                {
                    mainWindow.OutputPath = config.AppSettings.Settings["OutputPath"].Value;
                }
                else
                {
                    using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        dialog.Description = $"Select an output folder";
                        System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                        if (dialog.SelectedPath == "")
                        {
                            MessageBox.Show("Directory selected is invalid, please select the correct packages directory.");
                            return;
                        }
                        mainWindow.OutputPath = dialog.SelectedPath;
                        config.AppSettings.Settings.Add("OutputPath", mainWindow.OutputPath);
                        config.Save(ConfigurationSaveMode.Minimal);
                    }
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.FileName = "DestinyUnpackerCPP.exe";

            startInfo.Arguments = "-p \"" + config.AppSettings.Settings["PackagesPath"].Value + "\" -o \"" + mainWindow.OutputPath + "\" -f -m -h ";
            switch (config.AppSettings.Settings["AudioFormat"].Value.ToString())
            {
                case "Wem":
                    break;
                case "Ogg":
                    startInfo.Arguments += " -g ";
                    break;
                case "Wav":
                    startInfo.Arguments += " -w ";
                    break;
                default:
                    break;
            }

            try
            {
                Process exeProcess = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => log($"Error: {ex.Message}"));
                MessageBox.Show("Error: " + ex.Message);
            }
            Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
        }

        private void ExportAllInList_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < SecondaryList.Children.Count; i++)
            {
                ToggleButton ListItem = SecondaryList.Children[i] as ToggleButton;
                var item_text = (ListItem.Content as TextBlock).Text;
                string id = item_text.Split("\n")[0];
                if (GinsorIDList.Contains(id))
                {
                    SelectedWemIndex = i;
                    Export_Clicked(ListItem, e);
                }
                else
                {
                    ExportSegmentAudio();
                }
            }
            Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
        }

        private void ExportSegmentAudio()
        {
            List<VorbisWaveReader> VorbisReaders = new List<VorbisWaveReader>();
            for (int i = 0; i < GinsorIdsInSegment.Count; i++)
            {
                var GinsorID = GinsorIdsInSegment[i];
                byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dictlist[GinsorID].reference, extractor).Parse().data;
                VorbisWaveReader vorb = new VorbisWaveReader(new MemoryStream(ogg_data));
                VorbisReaders.Add(vorb);
            }
            var mix2 = new MixingWaveProvider32(VorbisReaders.ToArray());
            Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
            if (mainWindow.OutputPath == string.Empty)
            {
                if (config.AppSettings.Settings["OutputPath"] != null)
                {
                    mainWindow.OutputPath = config.AppSettings.Settings["OutputPath"].Value;
                }
                else
                {
                    using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        dialog.Description = $"Select an output folder";
                        System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                        if (dialog.SelectedPath == "")
                        {
                            MessageBox.Show("Directory selected is invalid, please select the correct packages directory.");
                            return;
                        }
                        mainWindow.OutputPath = dialog.SelectedPath;
                        config.AppSettings.Settings.Add("OutputPath", mainWindow.OutputPath);
                        config.Save(ConfigurationSaveMode.Minimal);
                    }
                }
            }
            string id = ((SecondaryList.Children[SelectedWemIndex] as ToggleButton).Content as TextBlock).Text.Split("\n")[0].Split(' ')[2];
            string output_path = mainWindow.OutputPath + "\\" + id;
            WaveFileWriter.CreateWaveFile(output_path + ".wav", mix2);
            Dispatcher.Invoke(() => log($"Exported segment to {output_path}.wav"));
        }

        private void PlaySegmentAudio(object sender, RoutedEventArgs e)
        {
            List<VorbisWaveReader> VorbisReaders = new List<VorbisWaveReader>();
            for (int i = 0; i < GinsorIdsInSegment.Count; i++)
            {
                var GinsorID = GinsorIdsInSegment[i];
                byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dictlist[GinsorID].reference, extractor).Parse().data;
                VorbisWaveReader vorb = new VorbisWaveReader(new MemoryStream(ogg_data));
                VorbisReaders.Add(vorb);
            }
            double duration = VorbisReaders[0].Length / VorbisReaders[0].WaveFormat.AverageBytesPerSecond;
            TrackInfoTextBlock.Text = "Track Info:\n    Length: " + duration.ToString("0.00") + "s";
            try
            {
                var mixer = new MixingSampleProvider(VorbisReaders.ToArray());
                waveOut.Dispose();
                waveOut = new WaveOut();
                waveOut.Init(mixer);
                Dispatcher.Invoke(() => PlaybackProgressBar.Maximum = duration * 1000);
                Dispatcher.Invoke(() => PlaybackProgressBar.Value = 0);

                waveOut.Play();
                Thread thread = new Thread(new ThreadStart(Run));
                thread.Start();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => log($"Error playing audio: {ex.Message}"));
                MessageBox.Show("Error playing audio: " + ex.Message);
            }
            if (export)
            {
                ExportSegmentAudio();
            }
            Dispatcher.Invoke(() => (sender as ToggleButton).IsChecked = false);
        }

        private void RegenerateListButton_Clicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This may take a while!");
            GinsorIDList.Clear();
            dictlist.Clear();
            Dictionary<string, List<uint>> id_to_segment = new Dictionary<string, List<uint>>();
            foreach (Package package in extractor.master_packages_stream())
            {
                if (!package.no_patch_id_name.Contains("audio"))
                {
                    continue;
                }
                for (int entry_index = 0; entry_index < package.entry_table().Count; entry_index++)
                {
                    Entry entry = package.entry_table()[entry_index];
                    if (entry.type == 26 && entry.subtype == 6)
                    {
                        byte[] bnkData = extractor.extract_entry_data(package, entry).data;
                        foreach (string gins in genList(bnkData, ref id_to_segment))
                        {
                            GinsorIDList.Add(gins);
                        }
                    }
                }
            }

            GinsorIDList = GinsorIDList.Distinct().ToList();
            foreach (string gins in GinsorIDList.ToArray())
            {
                List<uint> SegmentIds = id_to_segment[gins].Distinct().ToList();
                uint idx = 0;
                Package pkg = extractor.find_pkg_of_ginsid(gins, ref idx);
                GinsorIdEntry ginsid_entry = new GinsorIdEntry();
                ginsid_entry.reference = Utils.generate_reference_hash(pkg.package_id, idx);
                ginsid_entry.SegmentIDs = SegmentIds;
                dictlist[gins] = ginsid_entry;
            }

            var json = JsonConvert.SerializeObject(dictlist, Formatting.Indented);
            File.WriteAllText("GinsorID_ref_dict.json", json);
            File.WriteAllLines("OSTs.db", GinsorIDList);
            Dispatcher.Invoke(() => log("Loading..."));
            Dispatcher.Invoke(() => PrimaryList.Items.Clear());
            ShowList();
            Dispatcher.Invoke(() => log("All loaded."));
            MessageBox.Show("Done regenerating music list.");
            Dispatcher.Invoke(() => RegenerateListButton.IsChecked = false);
        }

        public List<string> genList(byte[] soundBankData, ref Dictionary<string, List<uint>> id_to_segment)
        {
            List<string> GinsorIDs = new List<string>();
            SoundBank memSoundBank = new InMemorySoundBank(soundBankData);
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

            var musicObjs = (hirc as SoundBankHierarchyChunk).Objects
                .Where(o => o is MusicObject)
                .Select(o => o as MusicObject);

            foreach (var obj in musicObjs)
            {
                if (obj.Type == HIRCObjectType.MusicSegment)
                {
                    var segment = obj as MusicSegment;
                    for (int i = 0; i < segment.ChildCount; i++)
                    {
                        foreach (var srch_obj in musicObjs)
                        {
                            if (srch_obj.Id == segment.ChildIds[i])
                            {
                                var track = srch_obj as MusicTrack;
                                for (int x = 0; x < track.SoundCount; x++)
                                {
                                    var sound = track.Sounds[x];
                                    var ginsid = ((uint)IPAddress.NetworkToHostOrder((int)sound.AudioId)).ToHex().ToUpper();
                                    Debug.WriteLine($"GinsorID of track {track.Id} (Parent Segment: {segment.Id}): {ginsid}");
                                    if (!id_to_segment.ContainsKey(ginsid) || id_to_segment[ginsid] == null)
                                    {
                                        id_to_segment[ginsid] = new List<uint>();
                                    }
                                    id_to_segment[ginsid].Add(segment.Id);
                                    GinsorIDs.Add(ginsid);
                                }
                            }
                        }
                    }
                }
            }
            return GinsorIDs;
        }

    }
}