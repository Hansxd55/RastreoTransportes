using System;
using System.Net.Http;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace AppChofer.Views;

public partial class MainView : UserControl
{
    private bool _enRuta = false;
    private DispatcherTimer _gpsTimer;
    private double _latitudActual = 25.7543; 
    private double _longitudActual = -102.9839;

    // 1. Creamos el "cartero" (HttpClient) que enviará los datos
    private static readonly HttpClient _httpClient = new HttpClient();

    public MainView()
    {
        InitializeComponent();
        
        _gpsTimer = new DispatcherTimer();
        _gpsTimer.Interval = TimeSpan.FromSeconds(3);
        _gpsTimer.Tick += OnGpsTimerTick;
    }

    private void OnBtnRutaClick(object sender, RoutedEventArgs e)
    {
        _enRuta = !_enRuta; 

        if (_enRuta)
        {
            BtnRuta.Content = "TERMINAR RUTA";
            BtnRuta.Background = Brushes.DarkRed;
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

    // 2. Agregamos 'async' para que el envío por internet no congele la pantalla del chofer
    private async void OnGpsTimerTick(object sender, EventArgs e)
    {
        Random rnd = new Random();
        _latitudActual += (rnd.NextDouble() - 0.5) * 0.0005; 
        _longitudActual += (rnd.NextDouble() - 0.5) * 0.0005;

        // 3. Preparamos el paquete JSON (usamos CultureInfo para que los decimales usen punto y no coma)
        string latStr = _latitudActual.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string lonStr = _longitudActual.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string jsonPayload = $"{{\"unidad\": \"Unidad-01\", \"latitud\": {latStr}, \"longitud\": {lonStr}}}";
        
        var contenido = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            // 4. Intentamos enviar el paquete a la API (usamos localhost en el puerto 5000 por ahora)
            TxtGps.Text = $"GPS - Lat: {_latitudActual:F5} | Lon: {_longitudActual:F5} (Enviando...)";
            
            HttpResponseMessage respuesta = await _httpClient.PostAsync("http://localhost:5000/api/coordenadas", contenido);
            
            if (respuesta.IsSuccessStatusCode)
            {
                TxtGps.Text = $"GPS - Lat: {_latitudActual:F5} | Lon: {_longitudActual:F5} (¡Enviado!)";
            }
        }
        catch (Exception)
        {
            // 5. Si la API está apagada o no hay internet, mostramos un error sin que la app crashee
            TxtGps.Text = $"GPS - Lat: {_latitudActual:F5} | Lon: {_longitudActual:F5} (Servidor apagado)";
        }
    }
}