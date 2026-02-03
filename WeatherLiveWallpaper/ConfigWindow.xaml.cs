using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace WeatherLiveWallpaper
{
    public partial class ConfigWindow : Window
    {
        // Carpeta en Mis Documentos para guardar videos
        private string libraryPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WeatherWidgetPro",
            "Videos"
        );

        public ConfigWindow()
        {
            InitializeComponent();

            // 1. Crear carpeta si no existe
            if (!Directory.Exists(libraryPath))
            {
                Directory.CreateDirectory(libraryPath);
            }

            CargarEstadoInicial();
            CargarListaVideos();
        }

        private void CargarEstadoInicial()
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                ToggleWidget.IsChecked = main.IsWidgetVisible;
            }
        }

        // --- GESTIÓN DE VIDEOS ---

        private void CargarListaVideos()
        {
            ListVideos.Items.Clear();
            if (Directory.Exists(libraryPath))
            {
                string[] archivos = Directory.GetFiles(libraryPath);
                foreach (string archivo in archivos)
                {
                    // Filtro simple
                    if (archivo.EndsWith(".mp4") || archivo.EndsWith(".wmv") || archivo.EndsWith(".avi"))
                    {
                        ListVideos.Items.Add(System.IO.Path.GetFileName(archivo));
                    }
                }
            }
        }

        private void BtnImportar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Videos|*.mp4;*.wmv;*.avi";

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string nombreArchivo = System.IO.Path.GetFileName(ofd.FileName);
                    string destino = System.IO.Path.Combine(libraryPath, nombreArchivo);

                    // Copiar a la carpeta de la biblioteca
                    File.Copy(ofd.FileName, destino, true);

                    CargarListaVideos(); // Refrescar
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al importar: " + ex.Message);
                }
            }
        }

        private void BtnUsar_Click(object sender, RoutedEventArgs e)
        {
            if (ListVideos.SelectedItem == null) return;

            string nombreSeleccionado = ListVideos.SelectedItem.ToString();
            string rutaCompleta = System.IO.Path.Combine(libraryPath, nombreSeleccionado);

            // Guardar en Settings
            Properties.Settings.Default.UserVideoPath = rutaCompleta;
            Properties.Settings.Default.Save();

            // Aplicar
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.RecargarVideo();
            }
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (ListVideos.SelectedItem == null) return;

            string nombreSeleccionado = ListVideos.SelectedItem.ToString();
            string rutaCompleta = System.IO.Path.Combine(libraryPath, nombreSeleccionado);

            if (MessageBox.Show("¿Eliminar video de la biblioteca?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    File.Delete(rutaCompleta);
                    CargarListaVideos();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al eliminar: " + ex.Message);
                }
            }
        }

        // --- GESTIÓN DE WIDGET ---

        private void ToggleWidget_Checked(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main) main.ToggleGadget(true);
        }

        private void ToggleWidget_Unchecked(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main) main.ToggleGadget(false);
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}