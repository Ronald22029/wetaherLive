using Newtonsoft.Json;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private string API_KEY = "9557d5e507e5cc015952f644f6e70d23"; // Key por defecto

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

        // Lluvia
        List<Gota> gotas = new List<Gota>();
        SKPaint pincelLluviaFondo, pincelRastro;
        SKBitmap texturaGota3D;
        float anchoLienzoLluvia = 0, altoLienzoLluvia = 0;
        bool mostrarLluvia = false;
        int tipoLluvia = 0;

        // Configuración
        private bool IsMetric = true;

        public bool IsWidgetVisible { get { return ElWidget.Visibility == Visibility.Visible; } }

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;

            // Cargar API Key personalizada si existe
            try
            {
                if (!string.IsNullOrEmpty(Properties.Settings.Default.UserApiKey))
                    API_KEY = Properties.Settings.Default.UserApiKey;
            }
            catch { }

            relojTimer.Interval = TimeSpan.FromSeconds(1);
            relojTimer.Tick += (s, e) => {
                if (!_modoVistaPrevia)
                {
                    TxtHora.Text = DateTime.Now.ToString("HH:mm");
                    TxtFecha.Text = DateTime.Now.ToString("dddd, dd MMM");
                }
            };
            relojTimer.Start();

            climaTimer.Interval = TimeSpan.FromMinutes(15);
            climaTimer.Tick += async (s, e) => await ActualizarClima();
            climaTimer.Start();

            ConfigurarIconoBandeja();

            pincelLluviaFondo = new SKPaint { Color = SKColors.White.WithAlpha(40), IsAntialias = true, StrokeWidth = 0.5f };
            pincelRastro = new SKPaint { Color = SKColors.White.WithAlpha(15), IsAntialias = true, Style = SKPaintStyle.Fill };
            pincelRastro.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2);
            texturaGota3D = CrearTexturaGota3D(128);

            CompositionTarget.Rendering += (s, e) => LienzoLluvia.InvalidateVisual();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CrearVentanaVideoFondo();
            ForzarTamano();

            // Tamaño responsivo
            ElWidget.Width = SystemParameters.PrimaryScreenWidth * 0.22;
            ElWidget.Height = SystemParameters.PrimaryScreenHeight * 0.55;
            ElWidget.UpdateLayout();

            _scrollViewerTimeline = ObtenerHijoVisual<ScrollViewer>(ListaPronostico);

            TxtHora.Text = DateTime.Now.ToString("HH:mm");
            TxtFecha.Text = DateTime.Now.ToString("dddd, dd MMM");

            await Task.Delay(1000);
            await ActualizarClima();
        }

        public void RecargarVideo()
        {
            if (videoWindow != null) videoWindow.Close();
            CrearVentanaVideoFondo();
        }

        public void ToggleGadget(bool visible)
        {
            ElWidget.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public async void RecargarClima()
        {
            await ActualizarClima();
        }

        private void CrearVentanaVideoFondo()
        {
            try
            {
                string rutaVideo = Properties.Settings.Default.UserVideoPath;
                videoWindow = new Window();
                videoWindow.Title = "FondoVideo";
                videoWindow.WindowStyle = WindowStyle.None;
                videoWindow.ResizeMode = ResizeMode.NoResize;
                videoWindow.ShowInTaskbar = false;
                videoWindow.Background = Brushes.Black;
                videoWindow.BorderThickness = new Thickness(0);
                videoWindow.Left = 0; videoWindow.Top = 0;
                videoWindow.Width = SystemParameters.PrimaryScreenWidth;
                videoWindow.Height = SystemParameters.PrimaryScreenHeight;

                MediaElement me = new MediaElement();
                me.LoadedBehavior = MediaState.Manual;
                me.Stretch = Stretch.UniformToFill;
                me.IsMuted = true;
                me.MediaEnded += (s, e) => {
                    ((MediaElement)s).Position = TimeSpan.Zero;
                    ((MediaElement)s).Play();
                };

                if (!string.IsNullOrEmpty(rutaVideo) && System.IO.File.Exists(rutaVideo))
                {
                    me.Source = new Uri(rutaVideo);
                    me.Play();
                }

                videoWindow.Content = me;
                videoWindow.Show();

                IntPtr videoHwnd = new WindowInteropHelper(videoWindow).Handle;
                EnviarHandleAlFondo(videoHwnd);
            }
            catch { }
        }

        // --- SKIA SHARP (CORREGIDO) ---
        public class Gota { public float X, Y, Tamaño, VelocidadBase; public bool EsFondo; }

        private SKBitmap CrearTexturaGota3D(int tamaño)
        {
            SKBitmap b = new SKBitmap(tamaño, tamaño);
            using (SKCanvas c = new SKCanvas(b))
            {
                c.Clear(SKColors.Transparent);
                float r = (tamaño / 2f) - 2;
                SKPoint center = new SKPoint(tamaño / 2f, tamaño / 2f);

                using (var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White.WithAlpha(15) }) { c.DrawCircle(center, r, p); }
                using (var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f, Color = SKColors.White.WithAlpha(60) }) { c.DrawCircle(center, r, p); }

                // CORRECCIÓN APLICADA AQUÍ: Se usa 'p', no 'pHighlight'
                using (var p = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White.WithAlpha(220) })
                {
                    c.Save();
                    c.RotateDegrees(-45, center.X, center.Y);
                    c.DrawOval(new SKRect(center.X - r * 0.6f, center.Y - r * 0.7f, center.X + r * 0.2f, center.Y - r * 0.4f), p);
                    c.Restore();
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
                    if (g.EsFondo)
                    {
                        canvas.DrawLine(g.X, g.Y, g.X, g.Y + g.Tamaño * 2, pincelLluviaFondo);
                        g.Y += g.VelocidadBase;
                        if (g.Y > altoLienzoLluvia) { g.Y = -50; g.X = random.Next(0, (int)anchoLienzoLluvia); }
                    }
                    else
                    {
                        float velocidadActual = g.VelocidadBase * 0.15f;
                        if (tipoLluvia == 1) velocidadActual *= 0.6f;
                        g.Y += velocidadActual; g.Tamaño -= 0.015f;
                        if (g.Tamaño > 3)
                        {
                            var rectRastro = new SKRect(g.X, g.Y - g.Tamaño * 1.5f, g.X + g.Tamaño, g.Y);
                            canvas.DrawOval(rectRastro, pincelRastro);
                        }
                        canvas.DrawBitmap(texturaGota3D, new SKRect(g.X, g.Y, g.X + g.Tamaño, g.Y + g.Tamaño), paintBitmap);
                        if (g.Tamaño < 1f || g.Y > altoLienzoLluvia) gotas.RemoveAt(i);
                    }
                }
            }
        }

        private void InicializarLluvia(int cant) { for (int i = 0; i < cant; i++) gotas.Add(CrearGotaNueva(true)); }
        private Gota CrearGotaNueva(bool fondo)
        {
            float w = anchoLienzoLluvia > 0 ? anchoLienzoLluvia : 400; float h = altoLienzoLluvia > 0 ? altoLienzoLluvia : 500;
            if (fondo) return new Gota { X = random.Next(0, (int)w), Y = random.Next(0, (int)h), VelocidadBase = random.Next(20, 35), Tamaño = random.Next(2, 5), EsFondo = true };
            else
            {
                float startY = (random.NextDouble() > 0.3) ? random.Next(0, (int)(h * 0.8)) : -20;
                float startX = random.Next(0, (int)w);
                float tMin = (tipoLluvia == 1) ? 6 : 8; float tMax = (tipoLluvia == 1) ? 10 : 14;
                float tam = (float)(random.NextDouble() * (tMax - tMin) + tMin);
                return new Gota { X = startX, Y = startY, Tamaño = tam, VelocidadBase = tam * 0.1f, EsFondo = false };
            }
        }

        // --- CLIMA ---
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

            DateTime inicio = UnixToDateTime(listaApi[0].dt);
            DateTime fin = inicio.AddDays(2);

            for (int i = 0; i < listaApi.Count - 1; i++)
            {
                var itemActual = listaApi[i];
                var itemSiguiente = listaApi[i + 1];
                DateTime fechaActual = UnixToDateTime(itemActual.dt);
                DateTime fechaSiguiente = UnixToDateTime(itemSiguiente.dt);

                if (fechaActual > fin) break;

                while (fechaActual < fechaSiguiente)
                {
                    if (fechaActual > inicio)
                    {
                        var itemSimulado = itemActual;
                        itemSimulado.dt = ((DateTimeOffset)fechaActual).ToUnixTimeSeconds();
                        resultado.Add(new ItemPronostico { Hora = fechaActual.ToString("HH:mm"), Temp = Math.Round(itemActual.main.temp) + simbolo, IconoPath = DeterminarIcono(itemSimulado, ciudadInfo), DataRaw = itemActual });
                    }
                    fechaActual = fechaActual.AddHours(1);
                }
            }
            return resultado;
        }

        private string DeterminarIcono(ForecastItem item, CityInfo ciudad)
        {
            string condicion = item.weather[0].main;
            string descripcion = item.weather[0].description.ToLower();
            DateTime horaItem = UnixToDateTime(item.dt);
            DateTime amanecerHoy = UnixToDateTime(ciudad.sunrise);
            DateTime atardecerHoy = UnixToDateTime(ciudad.sunset);
            DateTime amanecerItem = new DateTime(horaItem.Year, horaItem.Month, horaItem.Day, amanecerHoy.Hour, amanecerHoy.Minute, 0);
            DateTime atardecerItem = new DateTime(horaItem.Year, horaItem.Month, horaItem.Day, atardecerHoy.Hour, atardecerHoy.Minute, 0);
            bool esNoche = (horaItem < amanecerItem || horaItem > atardecerItem);

            if (condicion == "Thunderstorm") return "iconorayos.png";
            if (condicion == "Drizzle" || condicion == "Rain") return "iconolluvia.png";
            if (condicion == "Snow") return "icononieve.png";
            if (esNoche) return "icononubeluna.png";
            else
            {
                if (condicion == "Clear") return "iconosol.png";
                if (descripcion.Contains("broken") || descripcion.Contains("overcast")) return "icononube.png";
                return "iconosolnube.png";
            }
        }

        private void ListaPronostico_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaPronostico.SelectedItem is ItemPronostico seleccionado)
            {
                var datosItem = seleccionado.DataRaw;
                string simbolo = IsMetric ? "°" : "°F";
                string windUnit = IsMetric ? "m/s" : "mph";

                TxtTemp.Text = Math.Round(datosItem.main.temp).ToString() + simbolo;
                TxtClimaDesc.Text = char.ToUpper(datosItem.weather[0].description[0]) + datosItem.weather[0].description.Substring(1);
                TxtSensacion.Text = $"Sensación: {Math.Round(datosItem.main.feels_like)}°";
                TxtHumedad.Text = $"{datosItem.main.humidity}%";
                TxtViento.Text = $"{datosItem.wind.speed} {windUnit}";
                TxtPresion.Text = $"{datosItem.main.pressure} hPa";

                double dist = datosItem.visibility / 1000.0;
                string distUnit = "km";
                if (!IsMetric) { dist *= 0.621371; distUnit = "mi"; }
                TxtVisibilidad.Text = $"{dist:0.#} {distUnit}";

                if (seleccionado.Hora == "Ahora")
                {
                    _modoVistaPrevia = false;
                    TxtHora.Text = DateTime.Now.ToString("HH:mm");
                    TxtFecha.Text = DateTime.Now.ToString("dddd, dd MMM");
                    if (datosPronosticoGlobal != null) ActualizarEstadoPaisaje(datosItem, datosPronosticoGlobal.city, DateTime.Now);
                }
                else
                {
                    _modoVistaPrevia = true;
                    TxtHora.Text = seleccionado.Hora;
                    DateTime fechaReal = UnixToDateTime(datosItem.dt).Date;
                    TimeSpan horaSpan = TimeSpan.Parse(seleccionado.Hora);
                    TxtFecha.Text = fechaReal.Add(horaSpan).ToString("dddd, dd");
                    if (datosPronosticoGlobal != null) ActualizarEstadoPaisaje(datosItem, datosPronosticoGlobal.city, fechaReal.Add(horaSpan));
                }
            }
        }

        private void ActualizarEstadoPaisaje(ForecastItem climaItem, CityInfo ciudadInfo, DateTime fechaReferencia)
        {
            DateTime amanecerHoy = UnixToDateTime(ciudadInfo.sunrise);
            DateTime atardecerHoy = UnixToDateTime(ciudadInfo.sunset);
            string condicion = climaItem.weather[0].main;
            string descripcion = climaItem.weather[0].description.ToLower();
            string nuevoCielo = "cielo.png";
            string nuevaMontana = "montañasuelo.png";
            bool activarNubesAnimadas = false;
            bool esDeNoche = (fechaReferencia.TimeOfDay < amanecerHoy.TimeOfDay || fechaReferencia.TimeOfDay > atardecerHoy.TimeOfDay);

            if (esDeNoche) { nuevoCielo = "cielonoche.png"; nuevaMontana = "montañanoche.png"; }
            else
            {
                if (condicion == "Rain" || condicion == "Thunderstorm" || condicion == "Snow" || condicion == "Drizzle" || descripcion.Contains("broken") || descripcion.Contains("overcast")) { nuevoCielo = "nublado.png"; nuevaMontana = "montañalluvia.png"; }
                else { nuevoCielo = "cielo.png"; nuevaMontana = "montañasuelo.png"; }
            }

            activarNubesAnimadas = (condicion != "Clear");
            mostrarLluvia = false; tipoLluvia = 0;
            if (condicion == "Drizzle") { mostrarLluvia = true; tipoLluvia = 1; }
            else if (condicion == "Rain" || condicion == "Thunderstorm") { mostrarLluvia = true; tipoLluvia = 2; }

            if (nuevoCielo != cieloActual) { AnimarTransicion(ImgCieloActivo, ImgCieloSiguiente, nuevoCielo); cieloActual = nuevoCielo; }
            if (nuevaMontana != montanaActual) { AnimarTransicion(ImgMontanaActiva, ImgMontanaSiguiente, nuevaMontana); montanaActual = nuevaMontana; }

            GenerarNubesAnimadas(activarNubesAnimadas);
            LienzoLluvia.InvalidateVisual();
        }

        private void AnimarTransicion(Image imagenSaliente, Image imagenEntrante, string nuevaRutaResource)
        {
            try
            {
                imagenEntrante.Source = new BitmapImage(new Uri($"pack://application:,,,/{nuevaRutaResource}"));
                imagenEntrante.Opacity = 0;
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1.0));
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.0));
                fadeOut.Completed += (s, e) => { imagenSaliente.Source = imagenEntrante.Source; imagenSaliente.Opacity = 1; imagenEntrante.Opacity = 0; };
                imagenEntrante.BeginAnimation(Image.OpacityProperty, fadeIn);
                imagenSaliente.BeginAnimation(Image.OpacityProperty, fadeOut);
            }
            catch { }
        }

        private DateTime UnixToDateTime(long unix) { return DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().DateTime; }

        private void GenerarNubesAnimadas(bool activar)
        {
            CanvasNubesFondo.Children.Clear();
            if (!activar) return;
            BitmapImage imgNube;
            try { imgNube = new BitmapImage(new Uri("pack://application:,,,/nube.png")); } catch { return; }
            double anchoWidget = ElWidget.ActualWidth > 0 ? ElWidget.ActualWidth : 350;
            double altoWidget = ElWidget.ActualHeight > 0 ? ElWidget.ActualHeight : 500;
            int cantidad = 8;
            for (int i = 0; i < cantidad; i++)
            {
                Image nube = new Image { Source = imgNube };
                RenderOptions.SetBitmapScalingMode(nube, BitmapScalingMode.HighQuality);
                double escala = random.NextDouble() * 1.2 + 0.6;
                nube.Width = (anchoWidget * 0.3) * escala;
                nube.Opacity = random.NextDouble() * 0.4 + 0.3;
                double topY = random.Next(-20, (int)(altoWidget * 0.4));
                Canvas.SetTop(nube, topY);
                double startX = random.Next((int)-nube.Width, (int)anchoWidget);
                Canvas.SetLeft(nube, startX);
                CanvasNubesFondo.Children.Add(nube);
                DoubleAnimation anim = new DoubleAnimation { From = -nube.Width, To = anchoWidget + 50, Duration = new Duration(TimeSpan.FromSeconds(random.Next(50, 100))), RepeatBehavior = RepeatBehavior.Forever };
                anim.BeginTime = TimeSpan.FromSeconds(-random.Next(0, 60));
                nube.BeginAnimation(Canvas.LeftProperty, anim);
            }
        }

        protected override void OnClosed(EventArgs e) { if (iconoBandeja != null) iconoBandeja.Dispose(); base.OnClosed(e); }
        private void ForzarTamano() { this.Left = 0; this.Top = 0; this.Width = SystemParameters.PrimaryScreenWidth; this.Height = SystemParameters.PrimaryScreenHeight; }
        private void EnviarHandleAlFondo(IntPtr windowHandle) { IntPtr progman = FindWindow("Progman", null); SendMessage(progman, 0x052C, new IntPtr(0), IntPtr.Zero); IntPtr workerw = IntPtr.Zero; EnumWindows((tophandle, topparamhandle) => { IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null); if (p != IntPtr.Zero) { workerw = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null); } return true; }, IntPtr.Zero); if (workerw != IntPtr.Zero) SetParent(windowHandle, workerw); }
        private void ConfigurarIconoBandeja() { iconoBandeja = new Forms.NotifyIcon(); iconoBandeja.Icon = Drawing.SystemIcons.Application; iconoBandeja.Visible = true; iconoBandeja.Text = "Wallpaper Widget"; Forms.ContextMenu menu = new Forms.ContextMenu(); menu.MenuItems.Add("⚙ Configurar", (s, e) => { new ConfigWindow().ShowDialog(); }); menu.MenuItems.Add("❌ Salir", (s, e) => { if (videoWindow != null) videoWindow.Close(); iconoBandeja.Visible = false; Application.Current.Shutdown(); }); iconoBandeja.ContextMenu = menu; }

        private void ListaPronostico_PreviewMouseDown(object sender, MouseButtonEventArgs e) { if (_scrollViewerTimeline == null) _scrollViewerTimeline = ObtenerHijoVisual<ScrollViewer>(ListaPronostico); _isDragging = false; _startPoint = e.GetPosition(this); if (_scrollViewerTimeline != null) _startOffset = _scrollViewerTimeline.HorizontalOffset; ListaPronostico.CaptureMouse(); }
        private void ListaPronostico_PreviewMouseMove(object sender, MouseEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed && _scrollViewerTimeline != null) { Point currentPoint = e.GetPosition(this); double delta = _startPoint.X - currentPoint.X; if (Math.Abs(delta) > 5) { _isDragging = true; _scrollViewerTimeline.ScrollToHorizontalOffset(_startOffset + delta); } } }
        private void ListaPronostico_PreviewMouseUp(object sender, MouseButtonEventArgs e) { ListaPronostico.ReleaseMouseCapture(); if (!_isDragging) { var element = e.OriginalSource as FrameworkElement; if (element?.DataContext is ItemPronostico item) { ListaPronostico.SelectedItem = item; } } _isDragging = false; }
        private void ListaPronostico_PreviewMouseWheel(object sender, MouseWheelEventArgs e) { if (_scrollViewerTimeline == null) _scrollViewerTimeline = ObtenerHijoVisual<ScrollViewer>(ListaPronostico); if (_scrollViewerTimeline != null) { if (e.Delta > 0) _scrollViewerTimeline.LineLeft(); else _scrollViewerTimeline.LineRight(); e.Handled = true; } }
        public static T ObtenerHijoVisual<T>(DependencyObject parent) where T : DependencyObject { if (parent == null) return null; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T result) return result; var descendant = ObtenerHijoVisual<T>(child); if (descendant != null) return descendant; } return null; }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        public class ForecastResponse { public CityInfo city { get; set; } public List<ForecastItem> list { get; set; } }
        public class CityInfo { public string name { get; set; } public long sunrise { get; set; } public long sunset { get; set; } }
        public class ForecastItem { public long dt { get; set; } public MainInfo main { get; set; } public List<WeatherInfo> weather { get; set; } public WindInfo wind { get; set; } public int visibility { get; set; } }
        public class WeatherInfo { public string main { get; set; } public string description { get; set; } }
        public class MainInfo { public float temp { get; set; } public float feels_like { get; set; } public int pressure { get; set; } public int humidity { get; set; } }
        public class WindInfo { public float speed { get; set; } }
        public class ItemPronostico { public string Hora { get; set; } public string Temp { get; set; } public string IconoPath { get; set; } public ForecastItem DataRaw { get; set; } }
    }
}