using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using Android; 
using Android.Locations;
using Android.Content;
using System.Threading.Tasks;
using System;

namespace AppChofer.Android
{
    [Activity(
        Label = "iBus", 
        Theme = "@style/MyTheme.NoActionBar", 
        Icon = "@drawable/icon", 
        MainLauncher = true, 
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity<App>
    {
        private LocationManager? _locationManager;
        private MiBuscadorGps? _buscadorGps;
        private static (double Latitud, double Longitud)? _ultimaUbicacion = null;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // 1. Pedimos permisos
            if (CheckSelfPermission(Manifest.Permission.AccessFineLocation) != Permission.Granted)
            {
                RequestPermissions(new string[] 
                { 
                    Manifest.Permission.AccessFineLocation, 
                    Manifest.Permission.AccessCoarseLocation 
                }, 0);
            }

            // 2. Preparamos el motor del GPS
            _locationManager = (LocationManager)GetSystemService(Context.LocationService);
            _buscadorGps = new MiBuscadorGps();
            
            // Cada vez que el satélite detecte que te moviste, actualiza la variable
            _buscadorGps.UbicacionActualizada = (loc) => 
            {
                _ultimaUbicacion = (loc.Latitude, loc.Longitude);
            };

            // 3. Conectamos el puente para tu código visual
            AppChofer.Views.MainView.LeerGpsCelular = ObtenerUbicacionRealAsync;
        }

        protected override void OnResume()
        {
            base.OnResume();
            ActivarRastreoContinuo();
        }

        protected override void OnPause()
        {
            base.OnPause();
            // Para ahorrar batería si minimizas la app
            if (_locationManager != null && _buscadorGps != null)
            {
                _locationManager.RemoveUpdates(_buscadorGps);
            }
        }

        private void ActivarRastreoContinuo()
        {
            if (CheckSelfPermission(Manifest.Permission.AccessFineLocation) == Permission.Granted)
            {
                try
                {
                    // Le pedimos a Android que nos avise cada 1 segundo o cada 1 metro que te muevas
                    _locationManager?.RequestLocationUpdates(LocationManager.GpsProvider, 1000, 1, _buscadorGps);
                    _locationManager?.RequestLocationUpdates(LocationManager.NetworkProvider, 1000, 1, _buscadorGps);
                }
                catch (Exception) { }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
            {
                ActivarRastreoContinuo();
            }
        }

        private Task<(double Latitud, double Longitud)?> ObtenerUbicacionRealAsync()
        {
            return Task.FromResult(_ultimaUbicacion);
        }
    }

    // CLASE AUXILIAR QUE ESCUCHA EL SATÉLITE DE ANDROID
    public class MiBuscadorGps : Java.Lang.Object, ILocationListener
    {
        public Action<Location>? UbicacionActualizada;

        public void OnLocationChanged(Location location)
        {
            if (location != null)
            {
                UbicacionActualizada?.Invoke(location);
            }
        }

        public void OnProviderDisabled(string provider) { }
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string provider, Availability status, Bundle extras) { }
    }
}