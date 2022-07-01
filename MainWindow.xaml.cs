﻿using System.Windows;
using System.Configuration;
using System.ComponentModel;
using System.Windows.Controls;

namespace DestinyMusicViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public enum AudioFormat
    {
        [Description("Wem")]
        Wem = 1,
        [Description("Wav")]
        Wav = 2,
        [Description("Ogg")]
        Ogg = 3,
    }

    public partial class MainWindow : Window
    {
        

        public Configuration config = ConfigurationManager.OpenExeConfiguration(System.Windows.Forms.Application.ExecutablePath);
        public string PkgCacheName;
        public string PkgPathKey;
        public string OutputPath = string.Empty;
        public MainWindow()
        {
            InitializeComponent();
            InitialiseConfig();
        }

        private void InitialiseConfig()
        {
            PkgPathKey = "PackagesPathBL";

            if (config.AppSettings.Settings["AudioFormat"] != null)
            {
                if (config.AppSettings.Settings["AudioFormat"].Value == AudioFormat.Wem.ToString())
                {
                    Wem.IsChecked = true;
                }
                else if (config.AppSettings.Settings["AudioFormat"].Value == AudioFormat.Wav.ToString())
                {
                    Wav.IsChecked = true;
                }
                else if (config.AppSettings.Settings["AudioFormat"].Value == AudioFormat.Ogg.ToString())
                {
                    Ogg.IsChecked = true;
                }
                else
                {
                    MessageBox.Show("Incorrect value set for 'AudioFormat', defaulting to Wem");
                    Wem.IsChecked = true;
                }
            }
            if (config.AppSettings.Settings["AudioFormat"] == null)
            {
                Wem.IsChecked = true;
                config.AppSettings.Settings.Add("AudioFormat", "Wem");
            }
            
        }

        private void ChangeAudioType(object sender, RoutedEventArgs e)
        {
            AudioFormat AudioFormat;
            AudioFormat = AudioFormat.Wem;
            if (config.AppSettings.Settings["AudioFormat"] == null)
            {
                return;
                //config.AppSettings.Settings.Add("AudioFormat", AudioFormat.ToString());
            }

            RadioButton rb = sender as RadioButton;
            switch (rb.Name)
            {
                case "Wem":
                    AudioFormat = AudioFormat.Wem;
                    break;
                case "Wav":
                    AudioFormat = AudioFormat.Wav;
                    break;
                case "Ogg":
                    AudioFormat = AudioFormat.Ogg;
                    break;
                default:
                    AudioFormat = AudioFormat.Wem;
                    break;
            }
            config.AppSettings.Settings["AudioFormat"].Value = AudioFormat.ToString();
            config.Save(ConfigurationSaveMode.Minimal);
        }

        private void ClearOutputPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (config.AppSettings.Settings["OutputPath"] != null)
            {
                config.AppSettings.Settings.Remove("OutputPath");
                config.Save(ConfigurationSaveMode.Minimal);
            }
        }
        private void SetOutputPathButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = $"Select the output folder";
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (dialog.SelectedPath == "")
                {
                    MessageBox.Show("Directory selected is invalid, please select the correct packages directory.");
                    return;
                }

                config.AppSettings.Settings.Remove("OutputPath");
                config.AppSettings.Settings.Add("OutputPath", dialog.SelectedPath);
                config.Save(ConfigurationSaveMode.Minimal);
            }
        }
    }
}
