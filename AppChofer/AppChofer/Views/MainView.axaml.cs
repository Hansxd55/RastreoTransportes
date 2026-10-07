using System;
using System.Net.Http;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Mapsui.Projections;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;

namespace AppChofer.Views;

public partial class MainView : UserControl
{
    private bool _enRuta = false;
    private DispatcherTimer _gpsTimer;
    private double _latitudActual = 25.7543; 
    private double _longitudActual = -102.9839;
    private static readonly HttpClient _httpClient = new HttpClient();

    // 1. Declaramos el pin como variable para poder moverlo más adelante
    private PointFeature _pinTransporte;

    public MainView()
    {
        InitializeComponent();
        
        MapaControl.Map = new Mapsui.Map();
        MapaControl.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
        
        var (x, y) = SphericalMercator.FromLonLat(_longitudActual, _latitudActual);
        var centroSanPedro = new Mapsui.MPoint(x, y);
        MapaControl.Map.Navigator.CenterOnAndZoomTo(centroSanPedro, 15);
        
        // 2. CORRECCIÓN: Así se crea el pin rojo en la versión 5.1.0
        _pinTransporte = new PointFeature(new Mapsui.MPoint(x, y));
        _pinTransporte.Styles.Add(new SymbolStyle 
        { 
            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Red),
            SymbolScale = 0.8 
        });

        // 3. Metemos el pin en una capa transparente y la agregamos al mapa
        var capaPines = new MemoryLayer
        {
            Name = "Transportes",
            Features = new[] { _pinTransporte },
            Style = null // Esto asegura que respete el color rojo que le pusimos arriba
        };
        MapaControl.Map.Layers.Add(capaPines);
        
        _gpsTimer = new DispatcherTimer();
        _gpsTimer.Interval = TimeSpan.FromSeconds(3);
        _gpsTimer.Tick += OnGpsTimerTick;
    }

    // CORRECCIÓN: Agregamos object? para quitar la advertencia amarilla
    private void OnBtnRutaClick(object? sender, RoutedEventArgs e)
    {
        _enRuta = !_enRuta; 

        if (_enRuta)
        {
            BtnRuta.Content = "TERMINAR RUTA";
            BtnRuta.Background = Avalonia.Media.Brushes.DarkRed;
            TxtGps.Text = "Buscando señal GPS y conectando...";
            _gpsTimer.Start(); 
        }
        else
        {
            BtnRuta.Content = "INICIAR RUTA";
            BtnRuta.Background = SolidColorBrush.Parse("#2E8B57");
            TxtGps.Text = "Ruta detenida. GPS inactivo.";
            _gpsTimer.Stop(); 
        }
    }

    // CORRECCIÓN: Agregamos object? para quitar la advertencia amarilla
    private async void OnGpsTimerTick(object? sender, EventArgs e)
    {
        Random rnd = new Random();
        // Simulamos el movimiento del camión
        _latitudActual += (rnd.NextDouble() - 0.5) * 0.0005; 
        _longitudActual += (rnd.NextDouble() - 0.5) * 0.0005;

        // 4. Actualizamos la posición del pin visualmente
        var (nuevoX, nuevoY) = SphericalMercator.FromLonLat(_longitudActual, _latitudActual);
        _pinTransporte.Point.X = nuevoX;
        _pinTransporte.Point.Y = nuevoY;
        
        // Le ordenamos al lienzo del mapa que se vuelva a dibujar para reflejar el movimiento
        MapaControl.Refresh();

        string latStr = _latitudActual.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string lonStr = _longitudActual.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string jsonPayload = $"{{\"unidad\": \"Unidad-01\", \"latitud\": {latStr}, \"longitud\": {lonStr}}}";
        
        var contenido = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            TxtGps.Text = $"GPS - Lat: {_latitudActual:F5} | Lon: {_longitudActual:F5} (Enviando...)";
            HttpResponseMessage respuesta = await _httpClient.PostAsync("http://127.0.0.1:5000/api/coordenadas", contenido);
            
            if (respuesta.IsSuccessStatusCode)
            {
                TxtGps.Text = $"GPS - Lat: {_latitudActual:F5} | Lon: {_longitudActual:F5} (¡Enviado!)";
            }
        }
        catch (Exception)
        {
            TxtGps.Text = $"GPS - Lat: {_latitudActual:F5} | Lon: {_longitudActual:F5} (Servidor apagado)";
        }
    }
}