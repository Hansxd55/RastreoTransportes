namespace AppChofer.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _estadoConexion = "Desconectado";

    [ObservableProperty]
    private double _latitudActual = 25.754075; 

    [ObservableProperty]
    private double _longitudActual = -102.983584;

    public MainViewModel()
    {
        // Ya no iniciamos SignalR aquí. 
        // MainView.axaml.cs ahora es el único dueño de la conexión y del mapa.
    }
}
