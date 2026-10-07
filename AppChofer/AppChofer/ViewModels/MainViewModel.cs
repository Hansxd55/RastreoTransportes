namespace AppChofer.ViewModels;

using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;

public partial class MainViewModel : ObservableObject
{
    // ... el resto de tu código que ya pegaste ...
    private HubConnection? _hubConnection;

    [ObservableProperty]
    private string _estadoConexion = "Desconectado";

    [ObservableProperty]
    private double _latitudActual = 25.754075; // Coordenada por defecto de San Pedro

    [ObservableProperty]
    private double _longitudActual = -102.983584;

    public MainViewModel()
    {
        // Iniciamos la conexión al arrancar el ViewModel
        _ = InicializarSignalRAsync();
    }

    private async Task InicializarSignalRAsync()
    {
        // Apuntamos al túnel que abrimos en tu API local
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://127.0.0.1:5000/rastreohub")
            .WithAutomaticReconnect()
            .Build();

        // Nos suscribimos exactamente al mismo nombre de método que definimos en la API ("RecibirUbicacion")
        _hubConnection.On<UbicacionDto>("RecibirUbicacion", (ubicacion) =>
        {
            // Actualizamos las coordenadas en el hilo principal de la UI
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                LatitudActual = ubicacion.Latitud;
                LongitudActual = ubicacion.Longitud;
                
                Console.WriteLine($"[APP USUARIO] Coordenada recibida en tiempo real -> Lat: {LatitudActual}, Lon: {LongitudActual}");
            });
        });

        try
        {
            await _hubConnection.StartAsync();
            EstadoConexion = "CONECTADO EN VIVO";
        }
        catch (Exception ex)
        {
            EstadoConexion = "Error de conexión";
            Console.WriteLine($"Error conectando a SignalR: {ex.Message}");
        }
    }
}

// Modelo espejo para recibir los datos del servidor
public class UbicacionDto
{
    public string? Unidad { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
}