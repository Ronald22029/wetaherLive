using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WeatherLiveWallpaper
{
    // Clase para elementos de la lista (Video o Tema)
    public class LibraryItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; } // Si está vacío "", es el tema original
        public ImageSource Thumbnail { get; set; }
    }

    public partial class ConfigWindow : Window
    {
        // Rutas base
        private string videosPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WeatherWidgetPro", "Videos");
        private string themesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WeatherWidgetPro", "Themes");

        public ConfigWindow()
        {
            InitializeComponent();
            EnsureDirectories();
            CargarConfiguracion();
            CargarVideos();
            CargarTemas();
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(videosPath)) Directory.CreateDirectory(videosPath);
            if (!Directory.Exists(themesPath)) Directory.CreateDirectory(themesPath);
        }

        private void CargarConfiguracion()
        {
            // Cargar Ciudad
            try { TxtCiudad.Text = WeatherLiveWallpaper.Properties.Settings.Default.UserCity; } catch { TxtCiudad.Text = "Oruro"; }

            // Cargar Unidad
            try
            {
                if (WeatherLiveWallpaper.Properties.Settings.Default.TemperatureUnit == "Imperial") RadioF.IsChecked = true;
                else RadioC.IsChecked = true;
            }
            catch { RadioC.IsChecked = true; }

            // Cargar Estado Widget
            if (Application.Current.MainWindow is MainWindow main)
                CheckWidget.IsChecked = main.IsWidgetVisible;
        }

        // --- PESTAÑAS ---
        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelGeneral == null || PanelFondos == null || PanelWidgets == null) return;

            PanelGeneral.Visibility = Visibility.Hidden;
            PanelFondos.Visibility = Visibility.Hidden;
            PanelWidgets.Visibility = Visibility.Hidden;

            if (TabGeneral.IsChecked == true) PanelGeneral.Visibility = Visibility.Visible;
            else if (TabWidgets.IsChecked == true) PanelWidgets.Visibility = Visibility.Visible;
            else if (TabFondos.IsChecked == true) PanelFondos.Visibility = Visibility.Visible;
        }

        // --- GENERAL ---
        private void BtnGuardarCiudad_Click(object sender, RoutedEventArgs e)
        {
            WeatherLiveWallpaper.Properties.Settings.Default.UserCity = TxtCiudad.Text;
            WeatherLiveWallpaper.Properties.Settings.Default.Save();
            if (Application.Current.MainWindow is MainWindow main) main.RecargarClima();
            MessageBox.Show("Configuración guardada.");
        }

        private void RadioUnit_Checked(object sender, RoutedEventArgs e)
        {
            WeatherLiveWallpaper.Properties.Settings.Default.TemperatureUnit = (RadioF.IsChecked == true) ? "Imperial" : "Metric";
            WeatherLiveWallpaper.Properties.Settings.Default.Save();
            if (Application.Current.MainWindow is MainWindow main) main.RecargarClima();
        }

        private void CheckWidget_Changed(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main) main.ToggleGadget(CheckWidget.IsChecked == true);
        }

        // --- BIBLIOTECA DE WIDGETS (TEMAS) ---

        // NUEVA FUNCIÓN MÁGICA: Combina Cielo + Montaña para la miniatura
        private ImageSource CrearThumbnailCompuesto()
        {
            try
            {
                DrawingGroup group = new DrawingGroup();

                // 1. Capa Fondo (Cielo)
                BitmapImage cielo = new BitmapImage(new Uri("pack://application:,,,/cielo.png"));
                // Usamos un rectángulo base (ej. 300x500) para definir la proporción
                group.Children.Add(new ImageDrawing(cielo, new Rect(0, 0, 300, 500)));

                // 2. Capa Frente (Montaña)
                BitmapImage montana = new BitmapImage(new Uri("pack://application:,,,/montañasuelo.png"));
                group.Children.Add(new ImageDrawing(montana, new Rect(0, 0, 300, 500)));

                // Convertimos el dibujo compuesto en una imagen
                return new DrawingImage(group);
            }
            catch
            {
                // Si falla, volvemos a mostrar solo el cielo para que no se rompa
                try { return new BitmapImage(new Uri("pack://application:,,,/cielo.png")); } catch { return null; }
            }
        }

        private void CargarTemas()
        {
            ListThemes.Items.Clear();

            // 1. AGREGAR TEMA ORIGINAL (Usando la miniatura compuesta)
            try
            {
                var defaultTheme = new LibraryItem
                {
                    Name = "Original (Predeterminado)",
                    FullPath = "",
                    Thumbnail = CrearThumbnailCompuesto() // <--- AQUÍ USAMOS LA NUEVA FUNCIÓN
                };
                ListThemes.Items.Add(defaultTheme);

                string currentTheme = WeatherLiveWallpaper.Properties.Settings.Default.UserThemePath;
                if (string.IsNullOrEmpty(currentTheme)) ListThemes.SelectedItem = defaultTheme;
            }
            catch { }

            // 2. BUSCAR TEMAS EXTERNOS
            if (Directory.Exists(themesPath))
            {
                string[] carpetas = Directory.GetDirectories(themesPath);
                foreach (string carpeta in carpetas)
                {
                    string nombreTema = new DirectoryInfo(carpeta).Name;

                    // Buscamos miniatura (cielo.png o preview.png)
                    string thumbPath = Path.Combine(carpeta, "cielo.png");
                    if (!File.Exists(thumbPath)) thumbPath = Path.Combine(carpeta, "preview.png");

                    ImageSource thumb = null;
                    if (File.Exists(thumbPath))
                    {
                        try
                        {
                            BitmapImage bi = new BitmapImage();
                            bi.BeginInit();
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.UriSource = new Uri(thumbPath);
                            bi.EndInit();
                            thumb = bi;
                        }
                        catch { }
                    }

                    var item = new LibraryItem
                    {
                        Name = nombreTema,
                        FullPath = carpeta,
                        Thumbnail = thumb
                    };
                    ListThemes.Items.Add(item);

                    if (WeatherLiveWallpaper.Properties.Settings.Default.UserThemePath == carpeta)
                    {
                        ListThemes.SelectedItem = item;
                    }
                }
            }
        }

        private void BtnAplicarTema_Click(object sender, RoutedEventArgs e)
        {
            if (ListThemes.SelectedItem is LibraryItem item)
            {
                WeatherLiveWallpaper.Properties.Settings.Default.UserThemePath = item.FullPath;
                WeatherLiveWallpaper.Properties.Settings.Default.Save();

                if (Application.Current.MainWindow is MainWindow main) main.RecargarClima();
                MessageBox.Show($"Tema '{item.Name}' aplicado.");
            }
        }

        // --- BIBLIOTECA DE VIDEOS ---
        private void CargarVideos()
        {
            ListVideos.Items.Clear();
            if (Directory.Exists(videosPath))
            {
                string[] archivos = Directory.GetFiles(videosPath);
                foreach (string archivo in archivos)
                {
                    if (archivo.EndsWith(".mp4") || archivo.EndsWith(".wmv") || archivo.EndsWith(".avi"))
                    {
                        ListVideos.Items.Add(new LibraryItem
                        {
                            Name = Path.GetFileName(archivo),
                            FullPath = archivo,
                            Thumbnail = NativeThumbnailProvider.GetThumbnail(archivo)
                        });
                    }
                }
            }
        }

        private void BtnImportar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Videos|*.mp4;*.wmv;*.avi" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string destino = Path.Combine(videosPath, Path.GetFileName(ofd.FileName));
                    File.Copy(ofd.FileName, destino, true);
                    CargarVideos();
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (ListVideos.SelectedItem is LibraryItem item)
            {
                if (MessageBox.Show($"¿Eliminar {item.Name}?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try { File.Delete(item.FullPath); CargarVideos(); } catch { }
                }
            }
        }

        private void BtnUsarVideo_Click(object sender, RoutedEventArgs e)
        {
            if (ListVideos.SelectedItem is LibraryItem item)
            {
                WeatherLiveWallpaper.Properties.Settings.Default.UserVideoPath = item.FullPath;
                WeatherLiveWallpaper.Properties.Settings.Default.Save();
                if (Application.Current.MainWindow is MainWindow main) main.RecargarVideo();
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => this.Close();
    }

    // --- Helper de Miniaturas ---
    public static class NativeThumbnailProvider
    {
        private const string IShellItem2Guid = "7e9fb0d3-919f-4307-ab2e-9b1860310c93";
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        internal interface IShellItem { void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv); }
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        internal interface IShellItemImageFactory { void GetImage(POINT size, int flags, out IntPtr phbm); }
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int x; public int y; public POINT(int x, int y) { this.x = x; this.y = y; } }
        public static ImageSource GetThumbnail(string path)
        {
            if (!File.Exists(path)) return null;
            IntPtr hbitmap = IntPtr.Zero;
            try
            {
                Guid shellItem2Guid = new Guid(IShellItem2Guid);
                IShellItem nativeItem;
                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref shellItem2Guid, out nativeItem) == 0)
                {
                    var imageFactory = nativeItem as IShellItemImageFactory;
                    if (imageFactory != null) imageFactory.GetImage(new POINT(256, 256), 0, out hbitmap);
                }
                if (hbitmap != IntPtr.Zero)
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze(); return source;
                }
            }
            catch { }
            finally { if (hbitmap != IntPtr.Zero) DeleteObject(hbitmap); }
            return null;
        }
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);
    }
}