using System;
using System.Windows;
using Microsoft.Win32;

namespace WeatherLiveWallpaper
{
    public partial class ConfigWindow : Window
    {
        public ConfigWindow()
        {
            InitializeComponent();
            if (Application.Current.MainWindow is MainWindow main)
            {
                ToggleGadget.IsChecked = main.IsWidgetVisible;
            }
        }

        private void ToggleGadget_Checked(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main) main.ToggleGadget(true);
        }

        private void ToggleGadget_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main) main.ToggleGadget(false);
        }

        private void BtnCargarVideo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Videos|*.mp4;*.wmv;*.avi";
            if (ofd.ShowDialog() == true)
            {
                Properties.Settings.Default.UserVideoPath = ofd.FileName;
                Properties.Settings.Default.Save();
                if (Application.Current.MainWindow is MainWindow main) main.RecargarVideo();
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) { this.Close(); }
    }
}