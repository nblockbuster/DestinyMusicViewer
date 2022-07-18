using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace DestinyMusicViewer
{
    /// <summary>
    /// Interaction logic for ProgressView.xaml
    /// </summary>
    public partial class ProgressView : UserControl
    {
        private Queue<string> ProgressStages;
        private int TotalStageCount;

        public ProgressView()
        {
            InitializeComponent();
            Visibility = Visibility.Hidden;
        }

        private void UpdateProgress()
        {
            ProgressBar.Value = GetProgressPercentage();
            ProgressText.Text = GetCurrentStageName();
        }

        public void SetProgressStages(List<string> progressStages)
        {
            Dispatcher.Invoke(() =>
            {
                TotalStageCount = progressStages.Count;
                ProgressStages = new Queue<string>();
                foreach (var progressStage in progressStages)
                {
                    ProgressStages.Enqueue(progressStage);
                }

                Visibility = Visibility.Visible;
                UpdateProgress();
            });
        }

        public void CompleteStage()
        {
            Dispatcher.Invoke(() =>
            {
                string removed = ProgressStages.Dequeue();
                Debug.WriteLine($"Completed loading stage: {removed}");
                UpdateProgress();
                if (ProgressStages.Count == 0)
                {
                    Visibility = Visibility.Hidden;
                }
            });
        }

        public string GetCurrentStageName()
        {
            if (ProgressStages.Count > 0)
            {
                var stage = ProgressStages.Peek();
                Debug.WriteLine($"Starting loading stage: {stage}");
                return stage;
            }
            return "Loading";
        }

        public int GetProgressPercentage()
        {
            return 95 - 90 * ProgressStages.Count / TotalStageCount;
        }
    }
}
