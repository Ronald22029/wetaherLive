using Newtonsoft.Json;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WeatherLiveWallpaper
{
    public partial class MainWindow : Window
    {
        private string API_KEY = "9557d5e507e5cc015952f644f6e70d23";

        DispatcherTimer relojTimer = new DispatcherTimer();
        DispatcherTimer climaTimer = new DispatcherTimer();
        HttpClient clienteHttp = new HttpClient();
        private Forms.NotifyIcon iconoBandeja;
        Random random = new Random();

        private string cieloActual = "";
        private string montanaActual = "";
        private bool _modoVistaPrevia = false;
        private ForecastResponse datosPronosticoGlobal;
        private Point _startPoint;
        private double _startOffset;
        private bool _isDragging = false;
        private ScrollViewer _scrollViewerTimeline;
        private Window videoWindow;

        public class Gota { public float X, Y, Tamaño, VelocidadBase; public bool EsFondo; }
        List<Gota> gotas = new List<Gota>();
        SKPaint pincelLluviaFondo, pincelRastro;
        SKBitmap texturaGota3D;
        float anchoLienzoLluvia = 0, altoLienzoLluvia = 0;
        bool mostrarLluvia = false;
        int tipoLluvia = 0;

        private double rainSpeedFact = 1.0;
        private double rainSizeFact = 1.0;
        private double rainWindFact = 0.0;
        private bool IsMetric = true;
        public bool IsWidgetVisible { get { return ElWidget.Visibility == Visibility.Visible; } }

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            try { if (!string.IsNullOrEmpty(Properties.Settings.Default.UserApiKey)) API_KEY = Properties.Settings.Default.UserApiKey; } catch { }
            ActualizarParametrosLluvia();

            relojTimer.Interval = TimeSpan.FromSeconds(1);
            relojTimer.Tick += (s, e) => {
                if (!_modoVistaPrevia) { TxtHora.Text = DateTime.Now.ToString("HH:mm"); TxtFecha.Text = DateTime.Now.ToString("dddd, dd MMM"); }
            };
            relojTimer.Start();

            climaTimer.Interval = TimeSpan.FromMinutes(15);
            climaTimer.Tick += async (s, e) => await ActualizarClima();
            climaTimer.Start();

            ConfigurarIconoBandeja();

            // *** MEJORA DE VISIBILIDAD DE LLUVIA ***
            // Aumentamos opacidad del fondo (de 40 a 80)
            pincelLluviaFondo = new SKPaint { Color = SKColors.White.WithAlpha(80), IsAntialias = true, StrokeWidth = 1.0f };
            // Aumentamos opacidad del rastro (de 15 a 40)
            pincelRastro = new SKPaint { Color = SKColors.White.WithAlpha(40), IsAntialias = true, Style = SKPaintStyle.Fill };
            pincelRastro.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2);
            texturaGota3D = CrearTexturaGota3D(128);

            CompositionTarget.Rendering += (s, e) => LienzoLluvia.InvalidateVisual();
        }

        public void ActualizarParametrosLluvia()
        {
            try
            {
                rainSpeedFact = Properties.Settings.Default.RainSpeed;
                rainSizeFact = Properties.Settings.Default.RainSize;
                rainWindFact = Properties.Settings.Default.RainWind;
                if (rainSpeedFact <= 0) rainSpeedFact = 1.0;
                if (rainSizeFact <= 0) rainSizeFact = 1.0;
            }
            catch { }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CrearVentanaVideoFondo();
            ForzarTamano();
            ElWidget.Width = SystemParameters.PrimaryScreenWidth * 0.22;
            ElWidget.Height = SystemParameters.PrimaryScreenHeight * 0.55;
            _scrollViewerTimeline = ObtenerHijoVisual<ScrollViewer>(ListaPronostico);
            TxtHora.Text = DateTime.Now.ToString("HH:mm");
            TxtFecha.Text = DateTime.Now.ToString("dddd, dd MMM");
            await Task.Delay(1000);
            await ActualizarClima();
        }

        public void RecargarVideo() { if (videoWindow != null) videoWindow.Close(); CrearVentanaVideoFondo(); }
        public void ToggleGadget(bool visible) { ElWidget.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; }
        public async void RecargarClima() { await ActualizarClima(); }

        private void CrearVentanaVideoFondo()
        {
            try
            {
                string rutaVideo = Properties.Settings.Default.UserVideoPath;
                videoWindow = new Window { Title = "FondoVideo", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Background = Brushes.Black, BorderThickness = new Thickness(0) };
                videoWindow.Left = 0; videoWindow.Top = 0; videoWindow.Width = SystemParameters.PrimaryScreenWidth; videoWindow.Height = SystemParameters.PrimaryScreenHeight;
                MediaElement me = new MediaElement { LoadedBehavior = MediaState.Manual, Stretch = Stretch.UniformToFill, IsMuted = true };
                me.MediaEnded += (s, e) => { ((MediaElement)s).Position = TimeSpan.Zero; ((MediaElement)s).Play(); };
                if (!string.IsNullOrEmpty(rutaVideo) && System.IO.File.Exists(rutaVideo)) { me.Source = new Uri(rutaVideo); me.Play(); }
                videoWindow.Content = me; videoWindow.Show();
                IntPtr videoHwnd = new WindowInteropHelper(videoWindow).Handle;
                EnviarHandleAlFondo(videoHwnd);
            }
            catch { }
        }

        private SKBitmap CrearTexturaGota3D(int tamaño)
        {
            SKBitmap b = new SKBitmap(tamaño, tamaño);
            using (SKCanvas c = new SKCanvas(b))
            {
                c.Clear(SKColors.Transparent);
                float r = (tamaño / 2f) - 2; SKPoint center = new SKPoint(tamaño / 2f, tamaño / 2f);
                using (var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White.WithAlpha(30) }) { c.DrawCircle(center, r, p); }
                using (var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = SKColors.White.WithAlpha(100) }) { c.DrawCircle(center, r, p); }
                using (var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White.WithAlpha(240) })
                {
                    c.Save(); c.RotateDegrees(-45, center.X, center.Y);
                    c.DrawOval(new SKRect(center.X - r * 0.6f, center.Y - r * 0.7f, center.X + r * 0.2f, center.Y - r * 0.4f), p); c.Restore();
                }
            }
            return b;
        }

        private void OnPintarLluvia(object sender, SKPaintSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;
            anchoLienzoLluvia = e.Info.Width; altoLienzoLluvia = e.Info.Height;
            canvas.Clear(SKColors.Transparent);
            if (!mostrarLluvia || tipoLluvia == 0) return;

            int maxGotas = (tipoLluvia == 1) ? 100 : 300;
            if (gotas.Count < maxGotas) InicializarLluvia(maxGotas / 20);
            if (random.Next(0, (tipoLluvia == 1 ? 15 : 4)) == 0 && gotas.Count < maxGotas) gotas.Add(CrearGotaNueva(false));

            using (var paintBitmap = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High })
            {
                for (int i = gotas.Count - 1; i >= 0; i--)
                {
                    Gota g = gotas[i];
                    float vY = (float)(g.VelocidadBase * 0.15f * rainSpeedFact);
                    float vX = (float)(rainWindFact * 0.5f);
                    float t = (float)(g.Tamaño * rainSizeFact);
                    if (tipoLluvia == 1) vY *= 0.6f;

                    if (g.EsFondo)
                    {
                        canvas.DrawLine(g.X, g.Y, g.X + vX, g.Y + t * 2, pincelLluviaFondo);
                        g.Y += vY; g.X += vX * 0.2f;
                        if (g.Y > altoLienzoLluvia || g.X > anchoLienzoLluvia + 50 || g.X < -50) { g.Y = -50; g.X = random.Next(-50, (int)anchoLienzoLluvia + 50); }
                    }
                    else
                    {
                        g.Tamaño -= 0.015f * (float)rainSizeFact;
                        g.Y += vY; g.X += vX * 0.2f;
                        if (t > 3)
                        {
                            var rectRastro = new SKRect(g.X, g.Y - t * 1.5f, g.X + t + vX, g.Y);
                            canvas.DrawOval(rectRastro, pincelRastro);
                        }
                        canvas.DrawBitmap(texturaGota3D, new SKRect(g.X, g.Y, g.X + t, g.Y + t), paintBitmap);
                        if (g.Tamaño < 1f || g.Y > altoLienzoLluvia) gotas.RemoveAt(i);
                    }
                }
            }
        }

        private void InicializarLluvia(int cant) { for (int i = 0; i < cant; i++) gotas.Add(CrearGotaNueva(true)); }
        private Gota CrearGotaNueva(bool fondo)
        {
            float w = anchoLienzoLluvia > 0 ? anchoLienzoLluvia : 400; float h = altoLienzoLluvia > 0 ? altoLienzoLluvia : 500;
            // *** AUMENTO DE TAMAÑO BASE ***
            if (fondo) return new Gota { X = random.Next(-50, (int)w + 50), Y = random.Next(0, (int)h), VelocidadBase = random.Next(20, 35), Tamaño = random.Next(4, 8), EsFondo = true };
            else
            {
                float startY = (random.NextDouble() > 0.3) ? random.Next(0, (int)(h * 0.8)) : -20;
                float tam = (float)(random.NextDouble() * 8 + 6); // Entre 6 y 14px base
                return new Gota { X = random.Next(-50, (int)w + 50), Y = startY, Tamaño = tam, VelocidadBase = tam * 0.15f, EsFondo = false };
            }
        }

        private async Task ActualizarClima()
        {
            try
            {
                string ciudad = Properties.Settings.Default.UserCity;
                string unidadConfig = Properties.Settings.Default.TemperatureUnit;
                if (string.IsNullOrEmpty(ciudad)) ciudad = "Oruro";
                IsMetric = (unidadConfig != "Imperial");
                string apiUnit = IsMetric ? "metric" : "imperial";
                string url = $"https://api.openweathermap.org/data/2.5/forecast?q={ciudad}&appid={API_KEY}&units={apiUnit}&lang=es";
                string json = await clienteHttp.GetStringAsync(url);
                datosPronosticoGlobal = JsonConvert.DeserializeObject<ForecastResponse>(json);
                if (datosPronosticoGlobal != null)
                {
                    TxtCiudad.Text = datosPronosticoGlobal.city.name;
                    TxtAmanecer.Text = UnixToDateTime(datosPronosticoGlobal.city.sunrise).ToString("HH:mm");
                    TxtAtardecer.Text = UnixToDateTime(datosPronosticoGlobal.city.sunset).ToString("HH:mm");
                    var itemsUI = ProcesarPronosticoPorHora(datosPronosticoGlobal.list, datosPronosticoGlobal.city);
                    ListaPronostico.ItemsSource = itemsUI;
                    if (ListaPronostico.Items.Count > 0)
                    {
                        ListaPronostico.SelectedIndex = 0;
                        ActualizarEstadoPaisaje(datosPronosticoGlobal.list[0], datosPronosticoGlobal.city, DateTime.Now);
                    }
                }
            }
            catch { TxtCiudad.Text = "Error Red"; }
        }

        private List<ItemPronostico> ProcesarPronosticoPorHora(List<ForecastItem> listaApi, CityInfo ciudadInfo)
        {
            List<ItemPronostico> resultado = new List<ItemPronostico>();
            string simbolo = IsMetric ? "°" : "°F";
            if (listaApi.Count > 0)
                resultado.Add(new ItemPronostico { Hora = "Ahora", Temp = Math.Round(listaApi[0].main.temp) + simbolo, IconoPath = DeterminarIcono(listaApi[0], ciudadInfo), DataRaw = listaApi[0] });
            DateTime inicio = UnixToDateTime(listaApi[0].dt); DateTime fin = inicio.AddDays(2);
            for (int i = 0; i < listaApi.Count - 1; i++)
            {
                var actual = listaApi[i]; var sig = listaApi[i + 1];
                DateTime fActual = UnixToDateTime(actual.dt); DateTime fSig = UnixToDateTime(sig.dt);
                if (fActual > fin) break;
                while (fActual < fSig)
                {
                    if (fActual > inicio)
                    {
                        var sim = actual; sim.dt = ((DateTimeOffset)fActual).ToUnixTimeSeconds();
                        resultado.Add(new ItemPronostico { Hora = fActual.ToString("HH:mm"), Temp = Math.Round(actual.main.temp) + simbolo, IconoPath = DeterminarIcono(sim, ciudadInfo), DataRaw = actual });
                    }
                    fActual = fActual.AddHours(1);
                }
            }
            return resultado;
        }

        private string DeterminarIcono(ForecastItem item, CityInfo ciudad)
        {
            string c = item.weather[0].main; string d = item.weather[0].description.ToLower();
            DateTime h = UnixToDateTime(item.dt); DateTime sR = UnixToDateTime(ciudad.sunrise); DateTime sS = UnixToDateTime(ciudad.sunset);
            DateTime hR = new DateTime(h.Year, h.Month, h.Day, sR.Hour, sR.Minute, 0); DateTime hS = new DateTime(h.Year, h.Month, h.Day, sS.Hour, sS.Minute, 0);
            bool noche = (h < hR || h > hS);
            if (c == "Thunderstorm") return "iconorayos.png"; if (c == "Drizzle" || c == "Rain") return "iconolluvia.png"; if (c == "Snow") return "icononieve.png";
            if (noche) return "icononubeluna.png";
            else { if (c == "Clear") return "iconosol.png"; if (d.Contains("broken") || d.Contains("overcast")) return "icononube.png"; return "iconosolnube.png"; }
        }

        private void ListaPronostico_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaPronostico.SelectedItem is ItemPronostico sel)
            {
                var d = sel.DataRaw; string s = IsMetric ? "°" : "°F"; string wU = IsMetric ? "m/s" : "mph";
                TxtTemp.Text = Math.Round(d.main.temp) + s; TxtClimaDesc.Text = char.ToUpper(d.weather[0].description[0]) + d.weather[0].description.Substring(1);
                TxtSensacion.Text = $"Sensación: {Math.Round(d.main.feels_like)}°"; TxtHumedad.Text = $"{d.main.humidity}%"; TxtViento.Text = $"{d.wind.speed} {wU}"; TxtPresion.Text = $"{d.main.pressure} hPa";
                double dist = d.visibility / 1000.0; string dU = "km"; if (!IsMetric) { dist *= 0.621371; dU = "mi"; }
                TxtVisibilidad.Text = $"{dist:0.#} {dU}";
                if (sel.Hora == "Ahora") { _modoVistaPrevia = false; TxtHora.Text = DateTime.Now.ToString("HH:mm"); TxtFecha.Text = DateTime.Now.ToString("dddd, dd MMM"); if (datosPronosticoGlobal != null) ActualizarEstadoPaisaje(d, datosPronosticoGlobal.city, DateTime.Now); }
                else { _modoVistaPrevia = true; TxtHora.Text = sel.Hora; DateTime fReal = UnixToDateTime(d.dt).Date; TimeSpan span = TimeSpan.Parse(sel.Hora); TxtFecha.Text = fReal.Add(span).ToString("dddd, dd"); if (datosPronosticoGlobal != null) ActualizarEstadoPaisaje(d, datosPronosticoGlobal.city, fReal.Add(span)); }
            }
        }

        private void ActualizarEstadoPaisaje(ForecastItem item, CityInfo city, DateTime fecha)
        {
            DateTime sR = UnixToDateTime(city.sunrise); DateTime sS = UnixToDateTime(city.sunset);
            string c = item.weather[0].main; string d = item.weather[0].description.ToLower();
            string nC = "cielo.png"; string nM = "montañasuelo.png";
            bool noche = (fecha.TimeOfDay < sR.TimeOfDay || fecha.TimeOfDay > sS.TimeOfDay);
            if (noche) { nC = "cielonoche.png"; nM = "montañanoche.png"; }
            else { if (c == "Rain" || c == "Thunderstorm" || c == "Snow" || c == "Drizzle" || d.Contains("broken") || d.Contains("overcast")) { nC = "nublado.png"; nM = "montañalluvia.png"; } else { nC = "cielo.png"; nM = "montañasuelo.png"; } }

            mostrarLluvia = false; tipoLluvia = 0;
            if (c == "Drizzle") { mostrarLluvia = true; tipoLluvia = 1; } else if (c == "Rain" || c == "Thunderstorm") { mostrarLluvia = true; tipoLluvia = 2; }

            if (nC != cieloActual) { AnimarTransicion(ImgCieloActivo, ImgCieloSiguiente, nC); cieloActual = nC; }
            if (nM != montanaActual) { AnimarTransicion(ImgMontanaActiva, ImgMontanaSiguiente, nM); montanaActual = nM; }
            GenerarNubesAnimadas(c != "Clear");
            LienzoLluvia.InvalidateVisual();
        }

        private void AnimarTransicion(Image i1, Image i2, string path)
        {
            try
            {
                i2.Source = new BitmapImage(new Uri($"pack://application:,,,/{path}")); i2.Opacity = 0;
                DoubleAnimation fi = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1)); DoubleAnimation fo = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1));
                fo.Completed += (s, e) => { i1.Source = i2.Source; i1.Opacity = 1; i2.Opacity = 0; };
                i2.BeginAnimation(Image.OpacityProperty, fi); i1.BeginAnimation(Image.OpacityProperty, fo);
            }
            catch { }
        }

        private DateTime UnixToDateTime(long u) { return DateTimeOffset.FromUnixTimeSeconds(u).ToLocalTime().DateTime; }
        private void GenerarNubesAnimadas(bool on)
        {
            CanvasNubesFondo.Children.Clear(); if (!on) return;
            BitmapImage img; try { img = new BitmapImage(new Uri("pack://application:,,,/nube.png")); } catch { return; }
            double w = ElWidget.ActualWidth > 0 ? ElWidget.ActualWidth : 350;
            for (int i = 0; i < 8; i++)
            {
                Image n = new Image { Source = img }; RenderOptions.SetBitmapScalingMode(n, BitmapScalingMode.HighQuality);
                double s = random.NextDouble() * 1.2 + 0.6; n.Width = (w * 0.3) * s; n.Opacity = random.NextDouble() * 0.4 + 0.3;
                Canvas.SetTop(n, random.Next(-20, 200)); Canvas.SetLeft(n, random.Next(-50, (int)w));
                CanvasNubesFondo.Children.Add(n);
                DoubleAnimation a = new DoubleAnimation { From = -n.Width, To = w + 50, Duration = new Duration(TimeSpan.FromSeconds(random.Next(50, 100))), RepeatBehavior = RepeatBehavior.Forever };
                a.BeginTime = TimeSpan.FromSeconds(-random.Next(0, 60)); n.BeginAnimation(Canvas.LeftProperty, a);
            }
        }

        protected override void OnClosed(EventArgs e) { if (iconoBandeja != null) iconoBandeja.Dispose(); base.OnClosed(e); }
        private void ForzarTamano() { Left = 0; Top = 0; Width = SystemParameters.PrimaryScreenWidth; Height = SystemParameters.PrimaryScreenHeight; }
        private void EnviarHandleAlFondo(IntPtr h) { IntPtr p = FindWindow("Progman", null); SendMessage(p, 0x052C, new IntPtr(0), IntPtr.Zero); IntPtr w = IntPtr.Zero; EnumWindows((th, l) => { IntPtr s = FindWindowEx(th, IntPtr.Zero, "SHELLDLL_DefView", null); if (s != IntPtr.Zero) { w = FindWindowEx(IntPtr.Zero, th, "WorkerW", null); } return true; }, IntPtr.Zero); if (w != IntPtr.Zero) SetParent(h, w); }
        private void ConfigurarIconoBandeja() { iconoBandeja = new Forms.NotifyIcon { Icon = Drawing.SystemIcons.Application, Visible = true, Text = "Wallpaper Widget" }; Forms.ContextMenu m = new Forms.ContextMenu(); m.MenuItems.Add("⚙ Configurar", (s, e) => { new ConfigWindow().ShowDialog(); }); m.MenuItems.Add("❌ Salir", (s, e) => { if (videoWindow != null) videoWindow.Close(); iconoBandeja.Visible = false; Application.Current.Shutdown(); }); iconoBandeja.ContextMenu = m; }
        private void ListaPronostico_PreviewMouseDown(object s, MouseButtonEventArgs e) { if (_scrollViewerTimeline == null) _scrollViewerTimeline = ObtenerHijoVisual<ScrollViewer>(ListaPronostico); _isDragging = false; _startPoint = e.GetPosition(this); if (_scrollViewerTimeline != null) _startOffset = _scrollViewerTimeline.HorizontalOffset; ListaPronostico.CaptureMouse(); }
        private void ListaPronostico_PreviewMouseMove(object s, MouseEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed && _scrollViewerTimeline != null) { Point c = e.GetPosition(this); double d = _startPoint.X - c.X; if (Math.Abs(d) > 5) { _isDragging = true; _scrollViewerTimeline.ScrollToHorizontalOffset(_startOffset + d); } } }
        private void ListaPronostico_PreviewMouseUp(object s, MouseButtonEventArgs e) { ListaPronostico.ReleaseMouseCapture(); if (!_isDragging) { var el = e.OriginalSource as FrameworkElement; if (el?.DataContext is ItemPronostico i) ListaPronostico.SelectedItem = i; } _isDragging = false; }
        private void ListaPronostico_PreviewMouseWheel(object s, MouseWheelEventArgs e) { if (_scrollViewerTimeline == null) _scrollViewerTimeline = ObtenerHijoVisual<ScrollViewer>(ListaPronostico); if (_scrollViewerTimeline != null) { if (e.Delta > 0) _scrollViewerTimeline.LineLeft(); else _scrollViewerTimeline.LineRight(); e.Handled = true; } }
        public static T ObtenerHijoVisual<T>(DependencyObject p) where T : DependencyObject { if (p == null) return null; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(p); i++) { var c = VisualTreeHelper.GetChild(p, i); if (c is T r) return r; var d = ObtenerHijoVisual<T>(c); if (d != null) return d; } return null; }

        [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr c, IntPtr n);
        [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr c, string cn, string t);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lp, IntPtr l);
        public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);

        public class ForecastResponse { public CityInfo city { get; set; } public List<ForecastItem> list { get; set; } }
        public class CityInfo { public string name { get; set; } public long sunrise { get; set; } public long sunset { get; set; } }
        public class ForecastItem { public long dt { get; set; } public MainInfo main { get; set; } public List<WeatherInfo> weather { get; set; } public WindInfo wind { get; set; } public int visibility { get; set; } }
        public class WeatherInfo { public string main { get; set; } public string description { get; set; } }
        public class MainInfo { public float temp { get; set; } public float feels_like { get; set; } public int pressure { get; set; } public int humidity { get; set; } }
        public class WindInfo { public float speed { get; set; } }
        public class ItemPronostico { public string Hora { get; set; } public string Temp { get; set; } public string IconoPath { get; set; } public ForecastItem DataRaw { get; set; } }
    }
}