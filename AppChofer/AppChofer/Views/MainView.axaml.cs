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

namespace AppChofer.Views
{
    public partial class MainView : UserControl
    {
        private bool _enRuta = false;
        
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
            
            // --- 1. CONFIGURACIÓN DEL MAPA ---
            MapaControl.Map = new Mapsui.Map();
            MapaControl.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
            
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

        private async Task InicializarSignalRAsync()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/rastreohub")
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<UbicacionDto>("RecibirUbicacion", (ubicacion) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var (nuevoX, nuevoY) = SphericalMercator.FromLonLat(ubicacion.Longitud, ubicacion.Latitud);
                    _pinTransporte.Point.X = nuevoX;
                    _pinTransporte.Point.Y = nuevoY;
                    
                    MapaControl.Refresh();
                });
            });

            try
            {
                await _hubConnection.StartAsync();
            }
            catch (Exception) { }
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
        }

        private void OnTabUsuarioClick(object? sender, RoutedEventArgs e)
        {
            PanelUsuario.IsVisible = true;
            PanelChofer.IsVisible = false;
            
            BtnTabUsuario.Background = SolidColorBrush.Parse("#FFFFFF");
            BtnTabChofer.Background = SolidColorBrush.Parse("Transparent");
            
            TxtTitulo.Text = "UNIDAD EN CAMINO";
            TxtSubtitulo.Text = "Sigue tu ruta";
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
                BtnTabUsuario.Opacity = 0.3; // Lo hacemos semitransparente para que se vea "apagado"
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
                BtnTabUsuario.Opacity = 1.0; // Le regresamos su color normal
            }
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
                // Enviar la coordenada a tu servidor local
                var respuesta = await _httpClient.PostAsync("http://127.0.0.1:5000/api/coordenadas", contenido);
                
                if (respuesta.IsSuccessStatusCode)
                {
                    // Te mostrará en pantalla si el dato viene de la antena real o del simulador de PC
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
        public string Unidad { get; set; } = string.Empty;
        public double Latitud { get; set; }
        public double Longitud { get; set; }
    }
}