using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Android.OS;       // Necesario para Bundle
using Android.Runtime;  // Necesario para GeneratedEnum

namespace AppChofer.Android
{
    [Activity(
        Label = "AppChofer.Android",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity<App>
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            // 1. Inicializar la librería para que el celular permita pedir ubicación
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            // 2. Conectar nuestra clase nativa al "puente" de la vista principal
            AppChofer.Views.MainView.LeerGpsCelular = new GpsRealAndroid().ObtenerUbicacionActualAsync;
        }

        // 3. Este método es obligatorio para que Android procese la respuesta de "¿Permitir acceso a la ubicación?"
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}