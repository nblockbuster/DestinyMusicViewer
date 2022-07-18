using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DestinyMusicViewer
{
    /// <summary>
    /// Interaction logic for MainMenuView.xaml
    /// </summary>
    public partial class MainMenuView : UserControl
    {
        private static MainWindow _mainWindow = null;

        public MainMenuView()
        {
            InitializeComponent();
        }

        private void OnControlLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            _mainWindow = Window.GetWindow(this) as MainWindow;
        }

        private void BankViewButton_OnClick(object sender, RoutedEventArgs e)
        {
            BankView bankView = new BankView();
            _mainWindow.MakeNewTab("Soundbanks", bankView);
            _mainWindow.SetNewestTabSelected();
        }

        private void MusicViewButton_OnClick(object sender, RoutedEventArgs e)
        {
            MusicView musicView = new MusicView();
            _mainWindow.MakeNewTab("All Music", musicView);
            _mainWindow.SetNewestTabSelected();
        }
    }
}
