using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Android.OS;       
using Android.Runtime;  

namespace AppChofer.Android
{
    [Activity(Label = "iBus", Icon = "@drawable/Icon", Theme = "@style/Theme.AppCompat.NoActionBar", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : AvaloniaMainActivity<App>
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }

        // Le agregamos el signo de interrogación al Bundle? para quitar la advertencia
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            AppChofer.Views.MainView.LeerGpsCelular = new GpsRealAndroid().ObtenerUbicacionActualAsync;
        }

        // Quitamos la palabra "Android." antes de Permission para que no se confunda con tu proyecto
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}