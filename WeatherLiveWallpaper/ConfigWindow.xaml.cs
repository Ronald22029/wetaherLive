using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WeatherLiveWallpaper
{
    public class LibraryItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public ImageSource Thumbnail { get; set; }
    }

    public partial class ConfigWindow : Window
    {
        private string videosPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WeatherWidgetPro", "Videos");
        private string themesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WeatherWidgetPro", "Themes");
        private List<MainWindow.Gota> previewGotas = new List<MainWindow.Gota>();
        private Random rnd = new Random();

        public ConfigWindow()
        {
            InitializeComponent();
            EnsureDirectories();
            CargarConfiguracion();
            CargarVideos();
            CargarTemas();
            CompositionTarget.Rendering += (s, e) => PreviewLluvia.InvalidateVisual();
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(videosPath)) Directory.CreateDirectory(videosPath);
            if (!Directory.Exists(themesPath)) Directory.CreateDirectory(themesPath);
        }

        // --- FUNCIÓN PARA COMBINAR IMÁGENES (CIELO + MONTAÑA) ---
        private ImageSource GenerarThumbnailCombinado()
        {
            try
            {
                DrawingGroup group = new DrawingGroup();
                // 1. Capa de Cielo
                group.Children.Add(new ImageDrawing(new BitmapImage(new Uri("pack://application:,,,/cielo.png")), new Rect(0, 0, 300, 500)));
                // 2. Capa de Montaña (Superpuesta)
                group.Children.Add(new ImageDrawing(new BitmapImage(new Uri("pack://application:,,,/montañasuelo.png")), new Rect(0, 0, 300, 500)));

                // Crear imagen resultante
                DrawingImage combined = new DrawingImage(group);
                combined.Freeze(); // Importante para rendimiento
                return combined;
            }
            catch
            {
                // Fallback si algo sale mal
                try { return new BitmapImage(new Uri("pack://application:,,,/cielo.png")); } catch { return null; }
            }
        }

        private void CargarConfiguracion()
        {
            TxtCiudad.Text = Properties.Settings.Default.UserCity;
            string unit = Properties.Settings.Default.TemperatureUnit;
            if (unit == "Imperial") RadioF.IsChecked = true; else RadioC.IsChecked = true;
            if (Application.Current.MainWindow is MainWindow main) CheckWidget.IsChecked = main.IsWidgetVisible;

            SliderSpeed.Value = Properties.Settings.Default.RainSpeed == 0 ? 1.0 : Properties.Settings.Default.RainSpeed;
            SliderSize.Value = Properties.Settings.Default.RainSize == 0 ? 1.0 : Properties.Settings.Default.RainSize;
            SliderWind.Value = Properties.Settings.Default.RainWind;
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelGeneral == null || PanelFondos == null || PanelWidgets == null) return;
            PanelGeneral.Visibility = Visibility.Hidden;
            PanelWidgets.Visibility = Visibility.Hidden;
            PanelFondos.Visibility = Visibility.Hidden;
            if (TabGeneral.IsChecked == true) PanelGeneral.Visibility = Visibility.Visible;
            if (TabWidgets.IsChecked == true) PanelWidgets.Visibility = Visibility.Visible;
            if (TabFondos.IsChecked == true) PanelFondos.Visibility = Visibility.Visible;
        }

        private void BtnGuardarCiudad_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.UserCity = TxtCiudad.Text;
            Properties.Settings.Default.Save();
            if (Application.Current.MainWindow is MainWindow main) main.RecargarClima();
            MessageBox.Show("Guardado.");
        }

        private void RadioUnit_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            Properties.Settings.Default.TemperatureUnit = (RadioF.IsChecked == true) ? "Imperial" : "Metric";
            Properties.Settings.Default.Save();
            if (Application.Current.MainWindow is MainWindow main) main.RecargarClima();
        }

        private void CheckWidget_Changed(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main) main.ToggleGadget(CheckWidget.IsChecked == true);
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void RainParam_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            Properties.Settings.Default.RainSpeed = SliderSpeed.Value;
            Properties.Settings.Default.RainSize = SliderSize.Value;
            Properties.Settings.Default.RainWind = SliderWind.Value;
            Properties.Settings.Default.Save();
            if (Application.Current.MainWindow is MainWindow main) main.ActualizarParametrosLluvia();
        }

        private void OnPintarPreviewLluvia(object sender, SKPaintSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            int w = e.Info.Width; int h = e.Info.Height;

            if (previewGotas.Count < 30)
            {
                previewGotas.Add(new MainWindow.Gota
                {
                    X = rnd.Next(-20, w + 20),
                    Y = rnd.Next(-50, 0),
                    Tamaño = (float)(rnd.Next(3, 6) * SliderSize.Value), // Gotas más visibles en preview
                    VelocidadBase = (float)(rnd.Next(15, 25) * SliderSpeed.Value)
                });
            }

            using (var paint = new SKPaint { Color = SKColors.White.WithAlpha(220), StrokeWidth = 2.0f, IsAntialias = true })
            {
                for (int i = previewGotas.Count - 1; i >= 0; i--)
                {
                    var g = previewGotas[i];
                    float velX = (float)(SliderWind.Value * 0.2);
                    g.Y += g.VelocidadBase * 0.15f; g.X += velX;
                    canvas.DrawLine(g.X, g.Y, g.X + velX * 1.5f, g.Y + g.Tamaño * 2, paint);
                    if (g.Y > h) previewGotas.RemoveAt(i);
                }
            }
        }

        // --- CARGAR TEMAS (USANDO LA IMAGEN COMBINADA) ---
        private void CargarTemas()
        {
            ListThemes.Items.Clear();
            // Aquí usamos la función nueva para el tema Original
            ListThemes.Items.Add(new LibraryItem { Name = "Original", FullPath = "", Thumbnail = GenerarThumbnailCombinado() });

            if (Directory.Exists(themesPath))
            {
                foreach (var dir in Directory.GetDirectories(themesPath))
                {
                    string thumbPath = Path.Combine(dir, "preview.png");
                    ImageSource thumb = File.Exists(thumbPath) ? new BitmapImage(new Uri(thumbPath)) : null;
                    ListThemes.Items.Add(new LibraryItem { Name = new DirectoryInfo(dir).Name, FullPath = dir, Thumbnail = thumb });
                }
            }
        }

        private void BtnAplicarTema_Click(object sender, RoutedEventArgs e)
        {
            if (ListThemes.SelectedItem is LibraryItem item)
            {
                Properties.Settings.Default.UserThemePath = item.FullPath;
                Properties.Settings.Default.Save();
                if (Application.Current.MainWindow is MainWindow main) main.RecargarClima();
                MessageBox.Show("Tema aplicado.");
            }
        }

        private void CargarVideos()
        {
            ListVideos.Items.Clear();
            if (Directory.Exists(videosPath))
            {
                foreach (var file in Directory.GetFiles(videosPath))
                {
                    if (file.EndsWith(".mp4") || file.EndsWith(".wmv"))
                    {
                        ListVideos.Items.Add(new LibraryItem
                        {
                            Name = Path.GetFileName(file),
                            FullPath = file,
                            Thumbnail = NativeThumbnailProvider.GetThumbnail(file)
                        });
                    }
                }
            }
        }

        private void BtnImportar_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Videos|*.mp4;*.wmv" };
            if (ofd.ShowDialog() == true)
            {
                try { string dest = Path.Combine(videosPath, Path.GetFileName(ofd.FileName)); File.Copy(ofd.FileName, dest, true); CargarVideos(); } catch { }
            }
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (ListVideos.SelectedItem is LibraryItem item) { try { File.Delete(item.FullPath); CargarVideos(); } catch { } }
        }

        private void BtnUsarVideo_Click(object sender, RoutedEventArgs e)
        {
            if (ListVideos.SelectedItem is LibraryItem item)
            {
                Properties.Settings.Default.UserVideoPath = item.FullPath;
                Properties.Settings.Default.Save();
                if (Application.Current.MainWindow is MainWindow main) main.RecargarVideo();
                MessageBox.Show("Fondo de video aplicado.");
            }
        }
    }

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
                Guid guid = new Guid(IShellItem2Guid);
                IShellItem nativeItem;
                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref guid, out nativeItem) == 0)
                {
                    var factory = nativeItem as IShellItemImageFactory;
                    if (factory != null) factory.GetImage(new POINT(256, 144), 0, out hbitmap); // 16:9 thumbnail request
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
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr hObject);
    }
}