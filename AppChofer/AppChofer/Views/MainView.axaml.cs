using System;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Mapsui.Projections;
using Mapsui.Layers;
using Mapsui.Styles;

namespace AppChofer.Views;

public partial class MainView : UserControl
{
    private bool _enRuta = false;
    
    // Temporizadores
    private DispatcherTimer _choferTimer;
    private DispatcherTimer _pasajeroTimer;
    
    // Coordenadas simuladas del chofer
    private double _latitudChofer = 25.7543; 
    private double _longitudChofer = -102.9839;
    
    private static readonly HttpClient _httpClient = new HttpClient();
    private PointFeature _pinTransporte;

    public MainView()
    {
        InitializeComponent();
        
        // --- 1. CONFIGURACIÓN DEL MAPA ---
        MapaControl.Map = new Mapsui.Map();
        MapaControl.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
        
        var (x, y) = SphericalMercator.FromLonLat(-102.9839, 25.7543);
        var centroSanPedro = new Mapsui.MPoint(x, y);
        MapaControl.Map.Navigator.CenterOnAndZoomTo(centroSanPedro, 15);
        
        _pinTransporte = new PointFeature(new Mapsui.MPoint(x, y));
        _pinTransporte.Styles.Add(new SymbolStyle 
        { 
            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Green), 
            SymbolScale = 0.8 
        });

        var capaPines = new MemoryLayer
        {
            Name = "Transportes",
            Features = new[] { _pinTransporte },
            Style = null 
        };
        MapaControl.Map.Layers.Add(capaPines);
        
        // --- 2. CONFIGURACIÓN DE TIMERS ---
        _choferTimer = new DispatcherTimer();
        _choferTimer.Interval = TimeSpan.FromSeconds(3);
        _choferTimer.Tick += OnChoferTimerTick;

        _pasajeroTimer = new DispatcherTimer();
        _pasajeroTimer.Interval = TimeSpan.FromSeconds(2);
        _pasajeroTimer.Tick += OnPasajeroTimerTick;
        _pasajeroTimer.Start(); 
    }

    // --- 3. LÓGICA DE LAS PESTAÑAS (TABS) ---
    private void OnTabChoferClick(object? sender, RoutedEventArgs e)
    {
        PanelUsuario.IsVisible = false;
        PanelChofer.IsVisible = true;
        
        BtnTabChofer.Background = SolidColorBrush.Parse("#FFFFFF");
        BtnTabUsuario.Background = SolidColorBrush.Parse("Transparent");
        
        TxtTitulo.Text = "PANEL DE CONTROL";
        TxtSubtitulo.Text = "Transmisión GPS";
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

    // --- 4. LÓGICA DEL CHOFER ---
    private void OnBtnRutaClick(object? sender, RoutedEventArgs e)
    {
        _enRuta = !_enRuta; 

        if (_enRuta)
        {
            BtnRuta.Content = "TERMINAR RUTA";
            BtnRuta.Background = Avalonia.Media.Brushes.DarkRed;
            TxtGps.Text = "Conectando al servidor...";
            _choferTimer.Start(); 
        }
        else
        {
            BtnRuta.Content = "INICIAR RUTA";
            BtnRuta.Background = SolidColorBrush.Parse("#10B981");
            TxtGps.Text = "Ruta detenida. GPS inactivo.";
            _choferTimer.Stop(); 
        }
    }

    private async void OnChoferTimerTick(object? sender, EventArgs e)
    {
        Random rnd = new Random();
        _latitudChofer += (rnd.NextDouble() - 0.5) * 0.0005; 
        _longitudChofer += (rnd.NextDouble() - 0.5) * 0.0005;

        string latStr = _latitudChofer.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string lonStr = _longitudChofer.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string jsonPayload = $"{{\"unidad\": \"Unidad-01\", \"latitud\": {latStr}, \"longitud\": {lonStr}}}";
        
        var contenido = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            TxtGps.Text = $"Enviando... Lat: {_latitudChofer:F5}";
            var respuesta = await _httpClient.PostAsync("http://127.0.0.1:5000/api/coordenadas", contenido);
            
            if (respuesta.IsSuccessStatusCode)
            {
                TxtGps.Text = $"Enviado OK. Lat: {_latitudChofer:F5}";
            }
        }
        catch (Exception)
        {
            TxtGps.Text = "Error: Servidor apagado.";
        }
    }

    // --- 5. LÓGICA DEL PASAJERO ---
    private async void OnPasajeroTimerTick(object? sender, EventArgs e)
    {
        // Solo consume datos si el mapa está visible
        if (!PanelUsuario.IsVisible) return;

        try
        {
            string respuestaJson = await _httpClient.GetStringAsync("http://127.0.0.1:5000/api/coordenadas");
            JsonNode? nodo = JsonNode.Parse(respuestaJson);
            
            if (nodo != null)
            {
                double latAPI = (double)nodo["latitud"]!;
                double lonAPI = (double)nodo["longitud"]!;

                var (nuevoX, nuevoY) = SphericalMercator.FromLonLat(lonAPI, latAPI);
                _pinTransporte.Point.X = nuevoX;
                _pinTransporte.Point.Y = nuevoY;
                
                MapaControl.Refresh();
            }
        }
        catch (Exception) { }
    }
}