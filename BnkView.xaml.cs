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
using NAudio.Wave;
using NAudio.Vorbis;
using Tiger;
using Tiger.Formats;
using WwiseParserLib.Structures.Chunks;
using WwiseParserLib.Structures.Objects.HIRC;
using WwiseParserLib.Structures.SoundBanks;
using WwiseParserLib;

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

        //private static string cache_location = "packages_path.txt";
        private static string packages_path;
        private static Extractor extractor;
        public Dictionary<string, GinsorIdEntry> dictlist = new Dictionary<string, GinsorIdEntry>();
        public List<string> GinsorIDList = new List<string>();
        //public List<Package> Packages = new List<Package>();
        //Dictionary<uint, List<uint>> pkg_bnks_dict = new Dictionary<uint, List<uint>>();
        private MainWindow mainWindow = null;
        private static WaveOut waveOut = new WaveOut();
        public ConcurrentDictionary<string, Package> Packages = new ConcurrentDictionary<string, Package>();
        string CurrentGinsorId;
        int SelectedWemIndex = 0;
        bool export = false;
        VorbisWaveReader vorbis;

        //private uint selected_packageid;
        //private uint selected_entry_index;

        public void log(string message)
        {
            
            string time_string = DateTime.Now.ToString("dd/MMM/yyyy hh:mm:ss tt");
            logging_box.Text += $"[{time_string}]: {message}\n";
            if (logging_box.Text.Length > 4096)
            {
                logging_box.Text = $"[{time_string}]:Text box reached > 4096 characters. Cleared.\n";
            }
            logging_box_scroller.ScrollToBottom();
            File.AppendAllText("log.txt", $"[{time_string}]: {message}\n");
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
            VolSlider.Value = 1.0;
        }

        private void InitialiseConfig()
        {
            // Check for package path and load the list
            if (mainWindow.config.AppSettings.Settings["PackagesPath"] != null)
            {
                SelectPkgsDirectoryButton.Visibility = Visibility.Hidden;
                Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                packages_path = config.AppSettings.Settings["PackagesPath"].Value;
                extractor = new Extractor(packages_path, LoggerLevels.HighVerbouse);
                //GenPkgList();
                //var myResult = Task.Run(() => GenPkgList());
                Task.Factory.StartNew(() => { GenPkgList(); });
                //Task.Factory.StartNew(() => { LoadList(); });
                LoadList();
                //myResult = Task.Run(() => { LoadList(); });
                //t.Wait();
                //Dispatcher.Invoke(() => log("GUH!"));
                ShowList();
                //myResult = Task.Run(() => { ShowList(); });
                //t.Wait();
            }
            else
            {
                MessageBox.Show($"No package path found.");
            }
        }

        private void GenPkgList()
        {
            List<uint> PackageIDs = new List<uint>();
            if (File.Exists("GinsorID_ref_dict.json"))
            {
                dictlist = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GinsorIdEntry>>(File.ReadAllText("GinsorID_ref_dict.json"));
                foreach (var entry in dictlist)
                {
                    PackageIDs.Add(entry.Value.reference.package_id);
                }
            }
            var uniq = PackageIDs.Distinct().ToList();
            foreach (Package pkg in extractor.master_packages_stream())
            {
                if (uniq.Contains(pkg.package_id))
                {
                    Packages.AddOrUpdate(pkg.no_patch_id_name, pkg, (Key, OldValue) => OldValue);
                }
            }
            
        }


        private void LoadList()
        {
            if (File.Exists("GinsorID_ref_dict.json"))
            {
                SelectPkgsDirectoryButton.Visibility = Visibility.Hidden;
                long length = new FileInfo("GinsorID_ref_dict.json").Length;
                if (length == 0)
                {
                    log("OSTs.db empty.");
                    initialize();
                }
                Dispatcher.Invoke(() => log("Found GinsorID_ref_dict.json file."));
                Dispatcher.Invoke(() => log("Loading..."));
                dictlist = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GinsorIdEntry>>(File.ReadAllText("GinsorID_ref_dict.json"));
                //var logFile = File.ReadAllLines("GinsorID_ref_dict.json");
                foreach (var entry in dictlist)
                {
                    GinsorIDList.Add(entry.Key);
                }
                //GinsorIDList = new List<string>(logFile);
                Dispatcher.Invoke(() => log("Initializing extractor object"));
                Dispatcher.Invoke(() => log("Extractor initialized"));
                //foreach (var ginsor_id in GinsorIDList)
                //{
                    //uint entry_index = 0;
                    //Package ginsid_pkg = extractor.find_pkg_of_ginsid(ginsor_id, ref entry_index);
                    //string v1 = "0" + ginsid_pkg.header().package_id.ToString("X2");
                    //string v2 = v1 + "-" + entry_index.ToString("X2");
                    //MessageBox.Show(v1);
                    //Dispatcher.Invoke(() => log(Utils.generate_reference_hash(ginsid_pkg.package_id, entry_index).ToString()));
                    //dictlist[ginsor_id] = Utils.generate_reference_hash(ginsid_pkg.package_id, entry_index);
                //}
            }
            else
            {
                List<string> GinsorIDs = new List<string>();
                Dispatcher.Invoke(() => log("Initializing extractor object"));
                extractor = new Extractor(packages_path, LoggerLevels.HighVerbouse);
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
                            foreach (string gins in genList(bnkData))
                            {
                                GinsorIDs.Add(gins);
                            }
                        }
                    }
                }
                GC.Collect();
                if (GinsorIDs.Count == 0)
                {
                    throw new Exception("GinsorID table empty?");
                }
                var UniqueGinsorIDs = GinsorIDs.Distinct();
                Dispatcher.Invoke(() => log($"Raw GinsorID List Count: {GinsorIDs.Count} || Unique GinsorID List Count: {UniqueGinsorIDs.ToList().Count}"));
                File.WriteAllLines("OSTs.db", UniqueGinsorIDs);
                Dispatcher.Invoke(() => log($"Music Track Amount: {UniqueGinsorIDs.Count()}"));
                Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
                config.Save(ConfigurationSaveMode.Minimal);
            }
        }

        public void ShowList()
        {
            PrimaryList.Children.Clear();
            
            //for (int i = 0; i < GinsorIDList.Count(); i++)
            foreach (string GinsorId in GinsorIDList)
            {
                //string GinsorId = GinsorIDList[i];
                ToggleButton btn = new ToggleButton();
                Style style = Application.Current.Resources["Button_Command"] as Style;

                btn.Style = style;
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.VerticalAlignment = VerticalAlignment.Center;
                btn.Focusable = true;
                btn.Content = new TextBlock
                {
                    Text = GinsorId + "\nFrom Package " + dictlist[GinsorId].reference.package_id.ToString("X2") + "\nIn SegmentIDs: ",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                };
                btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
                btn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230));
                btn.Height = 75;
                btn.Click += GinsButton_Click;
                PrimaryList.Children.Add(btn);
                foreach (uint segment_id in dictlist[GinsorId].SegmentIDs)
                {
                    (btn.Content as TextBlock).Text += segment_id.ToString("X8") + " ";
                }
            }
            ScrollView.ScrollToTop();
        }

        private void GinsButton_Click(object sender, RoutedEventArgs e)
        {
            string ClickedGinsorId = ((sender as ToggleButton).Content as TextBlock).Text;
            CurrentGinsorId = ClickedGinsorId;
            PlayAndViewScript(ClickedGinsorId, sender, e);
            
        }

        private void PlayAndViewScript(string GinsorId, object sender, RoutedEventArgs e)
        {
            SelectedWemIndex = PrimaryList.Children.IndexOf(sender as ToggleButton);
            
            /*
            PrimaryList.Children.Clear();

            ToggleButton btn = new ToggleButton();
            Style style = Application.Current.Resources["Button_Command"] as Style;
            btn.Style = style;
            btn.HorizontalAlignment = HorizontalAlignment.Stretch;
            btn.VerticalAlignment = VerticalAlignment.Center;
            btn.Height = 40;
            btn.Focusable = false;
            btn.Content = "Go back to package list";
            btn.Padding = new Thickness(10, 5, 0, 5);
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
            btn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230));
            btn.HorizontalContentAlignment = HorizontalAlignment.Left;
            btn.Click += GoBack_Click;

            PrimaryList.Children.Add(btn);

            btn = new ToggleButton();
            btn.Focusable = true;

            btn.Content = new TextBlock
            {
                Text = GinsorId + "\nFrom PackageID: " + dictlist[GinsorId].package_id.ToString("X2"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            };

            btn.Style = style;
            btn.Height = 70;
            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
            btn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230));
            btn.HorizontalContentAlignment = HorizontalAlignment.Left;
            btn.Click += ClickPlay;
            PrimaryList.Children.Add(btn);
            */

            ClickPlay(GinsorId, sender, e);
            if (export)
            {
                Export_Clicked(sender, e);
            }
            //string ReferenceableAudioHash = dictlist[GinsorId].entry_a.ToString("X8");
        }

        private void ClickPlay(string GinsorId, object sender, RoutedEventArgs e)
        {
            foreach (ToggleButton button in PrimaryList.Children)
            {
                button.IsChecked = false;
            }
            if ((sender as ToggleButton).IsChecked == true)
                (sender as ToggleButton).IsChecked = false;
            if ((sender as ToggleButton).IsChecked == false)
                (sender as ToggleButton).IsChecked = true;
            //SelectedWemIndex = PrimaryList.Children.IndexOf((sender as ToggleButton));
            //(sender as ToggleButton).IsChecked = true;
            //string GinsorId = (((sender as ToggleButton).Content) as TextBlock).Text.Split("\n")[0];
            string gins_id = GinsorId.Split("\n")[0];
            byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dictlist[gins_id].reference, extractor).Parse().data;
            PlayOgg(ogg_data);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            waveOut.Stop();
            vorbis.Dispose();
            (sender as ToggleButton).IsChecked = false;
            //(PrimaryList.Children[SelectedWemIndex] as ToggleButton).IsChecked = false;
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {   
            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
                //(sender as ToggleButton).IsChecked = true;
            }
            else if (waveOut.PlaybackState == PlaybackState.Paused)
            {
                waveOut.Play();
                (sender as ToggleButton).IsChecked = false;
            }
        }
        
        private void PlayOgg(byte[] OggData)
        {
            vorbis = new VorbisWaveReader(new MemoryStream(OggData));
            double duration = vorbis.Length / vorbis.WaveFormat.AverageBytesPerSecond;
            TrackInfoTextBlock.Text = "Track Info:\n    Length: " + duration.ToString("0.00") + "s";// + "\n" + vorbis.TrackCount.ToString();
            try
            {
                waveOut.Dispose();
                waveOut = new WaveOut();
                waveOut.Init(vorbis);
                //sldrPlaybackProgress.Maximum = vorbis.Length;
                //sldrPlaybackProgress.Value = vorbis.Position;
                waveOut.Play();
            }
            catch (Exception ex)
            {
                log("Error Playing the audio");
            }
        }

        private void VolSliderUpdated(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            waveOut.Volume = (float)VolSlider.Value;
        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            ShowList();
        }

        public List<string> genList(byte[] soundBankData)
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
                                bool alreadyInList = false;
                                var track = srch_obj as MusicTrack;
                                for (int x = 0; x < track.SoundCount; x++)
                                {
                                    var sound = track.Sounds[x];
                                    var ginsid = ((uint)IPAddress.NetworkToHostOrder((int)sound.AudioId)).ToHex().ToUpper();
                                    if (GinsorIDs.Contains(ginsid))
                                    {
                                        alreadyInList = true;
                                        continue;
                                    }
                                    //Logger.log($"GinsorID of track {track.Id} (Parent Segment: {segment.Id}): {ginsid}", LoggerLevels.HighVerbouse);
                                    GinsorIDs.Add(ginsid);
                                }
                                if (!alreadyInList)
                                {
                                    for (int b = 0; b < track.TimeParameterCount; b++)
                                    {
                                        var TimeParam = track.TimeParameters[b];
                                        var EndOffset = TimeParam.EndOffset;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return GinsorIDs;
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
                    GenPkgList();
                    LoadList();
                    //Dispatcher.Invoke(() => log("GUH!"));
                    ShowList();
                }
            }
        }

        private void initialize()
        {
            var t = Task.Run(() =>
            {
                Dispatcher.Invoke(() => log("Initializing extractor object"));
                extractor = new Extractor(packages_path, LoggerLevels.HighVerbouse);
                Dispatcher.Invoke(() => log("Extractor initialized"));

                Dispatcher.Invoke(() => log("Building package database"));
            });
        }

        private bool SetPackagePath(string Path)
        {
            if (Path == "")
            {
                MessageBox.Show("Directory selected is invalid, please select the correct packages directory.");
                return false;
            }
            // Verify this is a valid path by checking to see if a .pkg file is inside
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

        /*
        private void updateOSTDB_buttonOnClick(object sender, RoutedEventArgs e)
    {
        List<string> GinsorIDs = new List<string>();
        Dispatcher.Invoke(() => log($"Generating new OSTs.db and comparing."));
        foreach (Package package in extractor.master_packages_stream())
        {
            if (!package.no_patch_id_name.Contains("audio") || package.no_patch_id_name.Contains("_en"))
            {
                continue;
            }

            Dispatcher.Invoke(() => log($"Analysing {package.no_patch_id_name}"));

            for (int entry_index = 0; entry_index < package.entry_table().Count; entry_index++)
            {
                Entry entry = package.entry_table()[entry_index];
                if (entry.type != 26 && entry.subtype != 6)
                {
                    continue;
                }
                //Dispatcher.Invoke(() => log($"\t|-{Utils.entry_name(package.package_id, (uint)entry_index)}"));
                GinsorIDs = GetAllGinsorIDsInBank(package.package_id, entry.entry_index);
            }
        }
        if (GinsorIDs.Count == 0)
        {
            Dispatcher.Invoke(() => log("GinsorID table empty!"));
            return;
            //MessageBox.Show("GinsorID Table Empty!", "No Wems Found!", MessageBoxButton.OK, MessageBoxImage.Error);
            //throw new Exception("GinsorID table empty?");
        }
        var UniqueGinsorIDs = GinsorIDs.Distinct();
        File.WriteAllLines("OSTs.db", UniqueGinsorIDs);
        Dispatcher.Invoke(() => log("Comparing OSTs.db.old to OSTs.db."));

        if (!File.Exists("OSTs.db.old"))
        {
            Dispatcher.Invoke(() => log("OSTs.db.old does not exist."));
            return;
        }
        List<string> GinsorID_New = new List<string>();
        List<string> GinsorID_Removed = new List<string>();
        string[] Old_OSTs_Lines;
        Old_OSTs_Lines = File.ReadAllLines("OSTs.db.old");
        foreach (string new_ginsid in UniqueGinsorIDs)
        {
            if (!Old_OSTs_Lines.Contains(new_ginsid))
            {
                GinsorID_New.Add($"+++ {new_ginsid}");
            }

        }
        foreach (string old_ginsid in Old_OSTs_Lines)
        {
            if (!UniqueGinsorIDs.Contains(old_ginsid))
            {
                GinsorID_Removed.Add($"--- {old_ginsid}");
            }
        }
        File.WriteAllLines("modified.txt", GinsorID_New);
        File.AppendAllLines("modified.txt", GinsorID_Removed);
        Dispatcher.Invoke(() => log("Removed and Added GinsorIDs stored in 'modified.txt'"));
    }
        */
        public List<string> GetAllGinsorIDsInBank(uint package_id, uint entry_index)
        {
            List<string> GinsorIDs = new List<string>();
            Tiger.Parsers.ParsedFile parsedFile;
            parsedFile = extractor.extract_entry_data(package_id, (int)entry_index);
            byte[] soundBankData = parsedFile.data;
            SoundBank memSoundBank = new InMemorySoundBank(soundBankData);
            var bkhd = memSoundBank.ParseChunk(SoundBankChunkType.BKHD);
            if (bkhd == null)
            {
                Dispatcher.Invoke(() => log($"{Utils.entry_name(package_id, (uint)entry_index)} does not have a valid Soundbank header."));
            }
            var hirc = memSoundBank.GetChunk(SoundBankChunkType.HIRC);
            if (hirc == null)
            {
                Dispatcher.Invoke(() => log($"{Utils.entry_name(package_id, (uint)entry_index)} does not have a valid Hierarchy header."));
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
                                    //Console.WriteLine($"GinsorID of track {track.Id} (Parent Segment: {segment.Id}): {ginsid}");
                                    GinsorIDs.Add(ginsid);
                                }
                            }
                        }
                    }
                }
            }
            if (GinsorIDs.Count == 0)
            {
                Dispatcher.Invoke(() => log("GinsorID table empty?"));
            }
            var UniqueGinsorIDs = GinsorIDs.Distinct();
            Dispatcher.Invoke(() => log($"Music Track Amount: {UniqueGinsorIDs.Count()}"));
            return UniqueGinsorIDs.ToList();
        }
        
        private void SegmentSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                Search(true);
            }
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                Search(false);
            }
        }
        
        private void Search(bool seg)
        {
            if (seg)
            {
                ToggleButton btn = SegmentDisplayButton;
                if (btn.Content != null)
                {
                    if ((btn.Content as TextBlock).Text != "")
                        (btn.Content as TextBlock).Text = "";
                } 
                List<string> GinsorIdsInSegment = new List<string>();
                //Search for all keys in dictlist that contain SegmentSearchBox.Text (hex string) as a uint in the SegmentIDs value
                if (SegmentSearchBox.Text == "") return;
                var results = dictlist.Where(x => x.Value.SegmentIDs.Contains(Convert.ToUInt32(SegmentSearchBox.Text, 16)));
                foreach (var result in results)
                {
                    GinsorIdsInSegment.Add(result.Key);
                }

                //Style style = Application.Current.Resources["Button_Command"] as Style;

                //btn.Style = style;
                //btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                //btn.VerticalAlignment = VerticalAlignment.Center;
                //btn.Focusable = false;
                btn.Content = new TextBlock
                {
                    Text = "Segment Id: " + SegmentSearchBox.Text.ToUpper() + "\nContains GinsorIds: ",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                };
                //btn.Background = new SolidColorBrush(Color.FromRgb(99, 99, 99));
                //btn.Foreground = new SolidColorBrush(Color.FromRgb(221, 221, 221));
                //btn.Height = 75;
                //btn.Click += GinsButton_Click;
                //SecondaryList.Children.Add(btn);
                
                
                foreach (var InSegmentGinsorId in GinsorIdsInSegment)
                {
                    (btn.Content as TextBlock).Text += InSegmentGinsorId + " ";
                }

                /*
                foreach (var key in dictlist.Keys)
                {
                    if (dictlist[key].SegmentIDs.Contains(Convert.ToUInt32(SegmentSearchBox.Text, 16)))
                    {
                        foreach (var seg_id in dictlist[key].SegmentIDs.ToList())
                        {
                            ToggleButton btn = new ToggleButton();
                            Style style = Application.Current.Resources["Button_Command"] as Style;

                            btn.Style = style;
                            btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                            btn.VerticalAlignment = VerticalAlignment.Center;
                            btn.Focusable = true;
                            btn.Content = new TextBlock
                            {
                                Text = SegmentId_Hex + "\nContains GinsorIds: ",
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 13
                            };
                            btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
                            btn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230));
                            btn.Height = 75;
                            btn.Click += GinsButton_Click;
                            SecondaryList.Children.Add(btn);
                            (btn.Content as TextBlock).Text += key + " ";
                        }
                    }
                }
                */

                /*
                foreach (uint SegmentId in dictlist[GinsorId].SegmentIDs)
                {
                    string SegmentId_Hex = SegmentId.ToString("X8");
                    if (SegmentId_Hex.ToUpper() != SegmentSearchBox.Text.ToUpper())
                        continue;

                    ToggleButton btn = new ToggleButton();
                    Style style = Application.Current.Resources["Button_Command"] as Style;

                    btn.Style = style;
                    btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                    btn.VerticalAlignment = VerticalAlignment.Center;
                    btn.Focusable = true;
                    btn.Content = new TextBlock
                    {
                        Text = SegmentId_Hex + "\nContains GinsorIds: ",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13
                    };
                    btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
                    btn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230));
                    btn.Height = 75;
                    btn.Click += GinsButton_Click;
                    PrimaryList.Children.Add(btn);

                    //search for all keys in dictlist that have the same segment id as the current segment id
                    

                    foreach (uint segid in dictlist[GinsorId].SegmentIDs)
                    {
                        (btn.Content as TextBlock).Text += segid.ToString("X8") + " ";
                    }
                }
                */
            }
            else
            {
                foreach (ToggleButton button in PrimaryList.Children)
                {
                    string GinsorId = (button.Content as TextBlock).Text.Split("\n")[0];
                    if (GinsorId.ToUpper().Contains(SearchBox.Text.ToUpper()))
                    {
                        button.BringIntoView();
                        button.IsChecked = true;
                    }
                }
            }
        }

        /*
        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            ClearSearchButton.Visibility = Visibility.Hidden;
            (sender as ToggleButton).IsChecked = false;
        }

        private void ClearSegmentSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SegmentSearchBox.Text = string.Empty;
            ClearSearchButton.Visibility = Visibility.Hidden;
            (sender as ToggleButton).IsChecked = false;
        }
        */
        /*
        private void sldrPlaybackProgressChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            long newPos = vorbis.Position + (long)(vorbis.WaveFormat.AverageBytesPerSecond * sldrPlaybackProgress.Value);
            //vorbis.Position += vorbis.WaveFormat.AverageBytesPerSecond;
            if ((newPos % vorbis.WaveFormat.BlockAlign) != 0)
                newPos -= newPos % vorbis.WaveFormat.BlockAlign;
            newPos = Math.Max(0, Math.Min(vorbis.Length, newPos));
            vorbis.Position = newPos;
        }
        */

        public void Export_Clicked(object sender, RoutedEventArgs e)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
            if (mainWindow.OutputPath == string.Empty)
            {
                //config.Save(ConfigurationSaveMode.Minimal);
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

            string gins_id = ((PrimaryList.Children[SelectedWemIndex] as ToggleButton).Content as TextBlock).Text.Split("\n")[0];
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
                Dispatcher.Invoke(() => log($"did it work? {output_path}"));
            }
            else if (config.AppSettings.Settings["AudioFormat"].Value.ToString() == "Ogg")
            {
                byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dictlist[gins_id].reference, extractor).Parse().data;
                using (var writer = new FileStream(output_path + ".ogg", FileMode.Create))
                {
                    writer.Write(ogg_data, 0, ogg_data.Length);
                }
                Dispatcher.Invoke(() => log($"did it work? {output_path}"));
            }
            else if (config.AppSettings.Settings["AudioFormat"].Value.ToString() == "Wav")
            {
                byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dictlist[gins_id].reference, extractor).Parse().data;
                using (var vorbis = new VorbisWaveReader(new MemoryStream(ogg_data)))
                {
                    WaveFileWriter.CreateWaveFile(output_path + ".wav", vorbis);
                }
                Dispatcher.Invoke(() => log($"did it work? {output_path}"));
            }
            else
            {
                MessageBox.Show("Audio format not supported");
            }
            Dispatcher.Invoke(() => log($"did it work? {output_path}"));
            (sender as ToggleButton).IsChecked = false;
        }

        private void ExportWhenClickedOn_Clicked(object sender, RoutedEventArgs e)
        {
            if (export == true)
            {
                export = false;
                (sender as ToggleButton).IsChecked = false;
            }
            else
            {
                export = true;
                (sender as ToggleButton).IsChecked = true;
            }
        }

        private void GenScript_Click(object sender, RoutedEventArgs e)
        {
            (sender as ToggleButton).IsChecked = false;
            //TODO: Add logic to generate a script for each playlist(?) a segment is in (get back to it later)
        }

        
        private void ExportAllButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO: Add logic to export all music files in order.

            Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
            if (mainWindow.OutputPath == string.Empty)
            {
                //config.Save(ConfigurationSaveMode.Minimal);
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
            foreach (var dict_entry in dictlist)
            {
                string output_path = mainWindow.OutputPath + "\\" + dict_entry.Key;
                if (config.AppSettings.Settings["AudioFormat"] == null)
                {
                    config.AppSettings.Settings.Add("AudioFormat", "Wem");
                }
                if (config.AppSettings.Settings["AudioFormat"].Value.ToString().ToLower() == "wem")
                {
                    byte[] wem_data = extractor.extract_entry_data(dict_entry.Value.reference).data;
                    using (var writer = new FileStream(output_path + ".wem", FileMode.Create))
                    {
                        writer.Write(wem_data, 0, wem_data.Length);
                    }
                }
                else if (config.AppSettings.Settings["AudioFormat"].Value.ToString().ToLower() == "ogg")
                {
                    byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dict_entry.Value.reference, extractor).Parse().data;
                    using (var writer = new FileStream(output_path + ".ogg", FileMode.Create))
                    {
                        writer.Write(ogg_data, 0, ogg_data.Length);
                    }
                }
                else if (config.AppSettings.Settings["AudioFormat"].Value.ToString().ToLower() == "wav")
                {
                    byte[] ogg_data = new Tiger.Parsers.RIFFAudioParser(dict_entry.Value.reference, extractor).Parse().data;
                    using (var vorbis = new VorbisWaveReader(new MemoryStream(ogg_data)))
                    {
                        //WaveFileWriter.CreateWaveFile(output_path + ".wav", vorbis);
                        MemoryStream OutputWavStream = new MemoryStream();
                        WaveFileWriter.WriteWavFileToStream(OutputWavStream, vorbis);
                        using (var writer = new FileStream(output_path + ".wav", FileMode.Create))
                        {
                            writer.Write(OutputWavStream.ToArray(), 0, OutputWavStream.ToArray().Length);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Audio format not supported");
                }
                Dispatcher.Invoke(() => log($"Exported {dict_entry.Key} to {output_path}.{config.AppSettings.Settings["AudioFormat"].Value.ToString().ToLower()}"));
                //GC.Collect();
            }
            (sender as ToggleButton).IsChecked = false;
        }
    }
}
