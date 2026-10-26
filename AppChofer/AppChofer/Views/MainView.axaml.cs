using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Mapsui.Projections;
using Mapsui.Layers;
using Mapsui.Styles;
using System.Linq;

namespace AppChofer.Views
{
    public partial class MainView : UserControl
    {
        private readonly string _servidorUrl = "https://ibus-sanpedro-c4edf6dhdeg2adbx.northcentralus-01.azurewebsites.net";
        private bool _enRuta = false;
        private bool _isDarkTheme;
        
        // Temporizador del Chofer 
        private DispatcherTimer _choferTimer;
        private double _latitudChofer = 25.7543; 
        private double _longitudChofer = -102.9839;
        
        // Conexión de SignalR para el Pasajero
        private HubConnection? _hubConnection;

        private static readonly HttpClient _httpClient = new HttpClient();
        private PointFeature _pinTransporte;

        // Variable puente para ejecutar código de Android desde aquí
        public static Func<Task<(double Latitud, double Longitud)?>> LeerGpsCelular { get; set; }

        public MainView()
        {
            InitializeComponent();
            ApplyTheme();
            
            // --- 1. CONFIGURACIÓN DEL MAPA ---
            MapaControl.Map = new Mapsui.Map();
            MapaControl.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer("AppChoferTransporte/1.0"));            
            var (x, y) = SphericalMercator.FromLonLat(-102.9839, 25.7543);
            var centroSanPedro = new Mapsui.MPoint(x, y);
            MapaControl.Map.Navigator.CenterOnAndZoomTo(centroSanPedro, 15);
            
            // Icono de camión reemplazando al punto verde
            _pinTransporte = new PointFeature(new Mapsui.MPoint(x, y));
            
            // 1. Capa de sombra/borde exterior (Blanco)
            _pinTransporte.Styles.Add(new SymbolStyle 
            { 
                Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.White), 
                SymbolScale = 0.5 
            });

            // 2. Capa central de ubicación (Verde vivo)
            _pinTransporte.Styles.Add(new SymbolStyle 
            { 
                Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#10B981")), 
                SymbolScale = 0.35 
            });

            var capaPines = new MemoryLayer
            {
                Name = "Transportes",
                Features = new[] { _pinTransporte },
                Style = null 
            };
            MapaControl.Map.Layers.Add(capaPines);
            
            // --- 2. TIMER DEL CHOFER ---
            _choferTimer = new DispatcherTimer();
            _choferTimer.Interval = TimeSpan.FromSeconds(1);
            _choferTimer.Tick += OnChoferTimerTick;

            // --- 3. INICIALIZAR SIGNALR PARA EL PASAJERO ---
            _ = InicializarSignalRAsync();
        }

        private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var ink = _isDarkTheme ? "#E6EFEC" : "#20312E";
            var muted = _isDarkTheme ? "#9EADA8" : "#6E7D79";
            var surface = _isDarkTheme ? "#24322F" : "#FFFFFF";
            var accent = _isDarkTheme ? "#6FC3AC" : "#267768";
            var accentSoft = _isDarkTheme ? "#294B43" : "#FFFFFF";
            var page = _isDarkTheme ? "#121B19" : "#EEF2EF";
            var controlSurface = _isDarkTheme ? "#C7202D2A" : "#B8FFFFFF";
            var cardSurface = _isDarkTheme ? "#D6202D2A" : "#C7FFFFFF";
            var shadowBorder = _isDarkTheme ? "#805D706A" : "#1420312E";

            RootShell.Background = SolidColorBrush.Parse(page);
            RoleSwitch.Background = SolidColorBrush.Parse(controlSurface);
            ThemeToggle.Background = SolidColorBrush.Parse(controlSurface);
            ThemeToggle.BorderBrush = SolidColorBrush.Parse(shadowBorder);
            ThemeIcon.Text = _isDarkTheme ? "☀" : "☾";
            ThemeIcon.Foreground = SolidColorBrush.Parse(accent);

            var choferSelected = PanelChofer.IsVisible;
            BtnTabChofer.Background = SolidColorBrush.Parse(choferSelected ? accentSoft : "Transparent");
            BtnTabChofer.Foreground = SolidColorBrush.Parse(choferSelected ? accent : muted);
            BtnTabChofer.BorderBrush = SolidColorBrush.Parse(choferSelected ? shadowBorder : "Transparent");
            BtnTabUsuario.Background = SolidColorBrush.Parse(choferSelected ? "Transparent" : accentSoft);
            BtnTabUsuario.Foreground = SolidColorBrush.Parse(choferSelected ? muted : accent);
            BtnTabUsuario.BorderBrush = SolidColorBrush.Parse(choferSelected ? "Transparent" : shadowBorder);

            TxtTitulo.Foreground = SolidColorBrush.Parse(accent);
            StatusCard.Background = SolidColorBrush.Parse(cardSurface);
            TxtSubtitulo.Foreground = SolidColorBrush.Parse(accent);
            PanelUsuario.Background = SolidColorBrush.Parse(surface);
            PanelUsuario.BorderBrush = SolidColorBrush.Parse(_isDarkTheme ? "#805D706A" : "#FFE5ECE9");
            PanelChofer.Background = SolidColorBrush.Parse(surface);
            ChoferPanelTitle.Foreground = SolidColorBrush.Parse(muted);
            GpsCard.Background = SolidColorBrush.Parse(controlSurface);
            TxtGps.Foreground = SolidColorBrush.Parse(accent);
            CapaPin.Background = SolidColorBrush.Parse(_isDarkTheme ? "#CC202D2A" : "#D6FFFFFF");
            PinDescription.Foreground = SolidColorBrush.Parse(muted);
            TxtBoxPin.Background = SolidColorBrush.Parse(controlSurface);
            TxtBoxPin.Foreground = SolidColorBrush.Parse(ink);
            TxtBoxPin.BorderBrush = SolidColorBrush.Parse(shadowBorder);
            BtnVerificarPin.Background = SolidColorBrush.Parse(accent);
            BtnRuta.Background = SolidColorBrush.Parse(_enRuta ? "#8B1E1E" : accent);
        }

        private async Task InicializarSignalRAsync()
        {
            // --- NUEVO: Evitamos duplicar conexiones si ya existe ---
            if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected)
            {
                return;
            }

            // 1. INICIALIZAMOS LA CONEXIÓN (CON PASE VIP Y BLINDAJE CONTRA CORTES)
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_servidorUrl}/rastreohub")
                .WithAutomaticReconnect() 
                .Build();

            // Chivatos visuales para saber si Android nos corta la conexión
            _hubConnection.Closed += async (error) => await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => TxtSubtitulo.Text = "Desconectado. Reconectando...");
            _hubConnection.Reconnecting += async (error) => await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => TxtSubtitulo.Text = "Reconectando SignalR...");
            _hubConnection.Reconnected += async (connectionId) => await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => TxtSubtitulo.Text = "¡Reconectado!");

          // 2. RECIBIMOS LOS DATOS Y LOS SACAMOS MANUALMENTE
            _hubConnection.On<object>("RecibirUbicacion", (datoCrudo) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        // 1. Sabemos que el dato llega como un elemento JSON
                        var jsonElement = (System.Text.Json.JsonElement)datoCrudo;
                        
                        // 2. Extraemos los números a la fuerza buscando su nombre
                        double latLlegada = jsonElement.GetProperty("latitud").GetDouble();
                        double lonLlegada = jsonElement.GetProperty("longitud").GetDouble();

                        // --- EL CHIVATO VISUAL ---
                        TxtSubtitulo.Text = $"GPS LLEGANDO: {latLlegada}";
                        
                        var (nuevoX, nuevoY) = Mapsui.Projections.SphericalMercator.FromLonLat(lonLlegada, latLlegada);
                        
                        // Movemos el pin
                        _pinTransporte.Point.X = nuevoX;
                        _pinTransporte.Point.Y = nuevoY;
                        
                        // Centramos la cámara
                        var nuevoCentro = new Mapsui.MPoint(nuevoX, nuevoY);
                        MapaControl.Map.Navigator.CenterOn(nuevoCentro);
                        
                        // Avisamos a la capa de pines
                        var capaPines = MapaControl.Map.Layers.FirstOrDefault(l => l.Name == "Transportes");
                        if (capaPines != null)
                        {
                            capaPines.DataHasChanged(); 
                        }
                        
                        // Refrescamos pantalla
                        MapaControl.Refresh();
                    }
                    catch (Exception ex)
                    {
                        TxtSubtitulo.Text = $"Error Lectura: {ex.Message}";
                    }
                }, Avalonia.Threading.DispatcherPriority.Normal);
            });
            // 3. ENCENDEMOS EL MAPA
            try
            {
                await _hubConnection.StartAsync();
                
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TxtSubtitulo.Text = $"Estado: {_hubConnection.State}";
                });
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TxtSubtitulo.Text = $"Error red: {ex.Message}";
                });
            }
        }

        // --- 4. LÓGICA DE LAS PESTAÑAS (TABS) ---
        private void OnTabChoferClick(object? sender, RoutedEventArgs e)
        {
            PanelUsuario.IsVisible = false;
            PanelChofer.IsVisible = true;
            
            BtnTabChofer.Background = SolidColorBrush.Parse("#FFFFFF");
            BtnTabUsuario.Background = SolidColorBrush.Parse("Transparent");
            
            TxtTitulo.Text = "PANEL DE CONTROL";
            TxtSubtitulo.Text = "Transmisión GPS";

            // Reiniciamos la capa del PIN
            CapaPin.IsVisible = true;
            ContenidoChofer.IsVisible = false;
            TxtBoxPin.Text = "";
            TxtErrorPin.Text = "";
            ApplyTheme();
        }

        private void OnTabUsuarioClick(object? sender, RoutedEventArgs e)
        {
            PanelUsuario.IsVisible = true;
            PanelChofer.IsVisible = false;
            
            BtnTabUsuario.Background = SolidColorBrush.Parse("#FFFFFF");
            BtnTabChofer.Background = SolidColorBrush.Parse("Transparent");
            
            TxtTitulo.Text = "UNIDAD EN CAMINO";
            
            // En lugar de tapar la pantalla con "Sigue tu ruta", mostramos el estado real
            TxtSubtitulo.Text = _hubConnection != null ? $"Estado actual: {_hubConnection.State}" : "Iniciando red...";

            // Si la conexión no existe o está desconectada, la iniciamos
            if (_hubConnection == null || _hubConnection.State == HubConnectionState.Disconnected)
            {
                _ = InicializarSignalRAsync();
            }

            ApplyTheme();
        }

        // --- 5. VALIDACIÓN DEL PIN DEL CHOFER ---
        private void OnVerificarPinClick(object? sender, RoutedEventArgs e)
        {
            string pinIngresado = TxtBoxPin.Text ?? string.Empty;

            if (pinIngresado == "chofer339")
            {
                CapaPin.IsVisible = false;
                ContenidoChofer.IsVisible = true;
                TxtErrorPin.Text = "";
            }
            else
            {
                TxtErrorPin.Text = "PIN incorrecto. Intente de nuevo.";
            }
        }

        // --- 6. LÓGICA DEL CHOFER ---
        private void OnBtnRutaClick(object? sender, RoutedEventArgs e)
        {
            _enRuta = !_enRuta; 

            if (_enRuta)
            {
                // Inicia la ruta
                BtnRuta.Content = "TERMINAR RUTA";
                BtnRuta.Background = Avalonia.Media.Brushes.DarkRed;
                TxtGps.Text = "Conectando al servidor...";
                _choferTimer.Start(); 
                
                // BLOQUEAR PESTAÑA DE USUARIO
                BtnTabUsuario.IsEnabled = false;
                BtnTabUsuario.Opacity = 0.3; 
            }
            else
            {
                // Termina la ruta
                BtnRuta.Content = "INICIAR RUTA";
                BtnRuta.Background = SolidColorBrush.Parse("#10B981");
                TxtGps.Text = "Ruta detenida. GPS inactivo.";
                _choferTimer.Stop(); 
                
                // DESBLOQUEAR PESTAÑA DE USUARIO
                BtnTabUsuario.IsEnabled = true;
                BtnTabUsuario.Opacity = 1.0; 
            }

            ApplyTheme();
        }

        private async void OnChoferTimerTick(object? sender, EventArgs e)
        {
            double latParaEnviar = _latitudChofer;
            double lonParaEnviar = _longitudChofer;
            bool usandoGpsReal = false;

            // 1. Verificamos si estamos en Android y leemos la antena física
            if (LeerGpsCelular != null)
            {
                TxtGps.Text = "Buscando satélite...";
                var coords = await LeerGpsCelular();
                
                if (coords != null)
                {
                    latParaEnviar = coords.Value.Latitud;
                    lonParaEnviar = coords.Value.Longitud;
                    usandoGpsReal = true;
                    
                    // Actualizamos nuestras variables por si se pierde la señal en el siguiente tick
                    _latitudChofer = latParaEnviar;
                    _longitudChofer = lonParaEnviar;
                }
            }

            // 2. Si no hay celular (estamos en la PC) o falló la señal, usamos el simulador
            if (!usandoGpsReal)
            {
                Random rnd = new Random();
                _latitudChofer += (rnd.NextDouble() - 0.5) * 0.0005; 
                _longitudChofer += (rnd.NextDouble() - 0.5) * 0.0005;
                latParaEnviar = _latitudChofer;
                lonParaEnviar = _longitudChofer;
            }

            // 3. Preparamos el paquete de datos
            string latStr = latParaEnviar.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lonStr = lonParaEnviar.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string jsonPayload = $"{{\"unidad\": \"Unidad-01\", \"latitud\": {latStr}, \"longitud\": {lonStr}}}";
            
            var contenido = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                // USAMOS LA VARIABLE GLOBAL PARA ENVIAR EL DATO AL SERVIDOR
                var respuesta = await _httpClient.PostAsync($"{_servidorUrl}/api/coordenadas", contenido);
                
                if (respuesta.IsSuccessStatusCode)
                {
                    string origen = usandoGpsReal ? "GPS CEL" : "SIMULADOR PC";
                    TxtGps.Text = $"[{origen}] OK: {latParaEnviar:F5}";
                }
            }
            catch (Exception)
            {
                TxtGps.Text = "Error: Servidor apagado o inaccesible.";
            }
        }
    }

    public class UbicacionDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("latitud")]
        public double Latitud { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("longitud")]
        public double Longitud { get; set; }
    }
}