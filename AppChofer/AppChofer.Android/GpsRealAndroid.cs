using System;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace AppChofer.Android
{
    public class GpsRealAndroid
    {
        public async Task<(double Latitud, double Longitud)?> ObtenerUbicacionActualAsync()
        {
            try
            {
                // Le pedimos al celular la ubicación con alta precisión (GPS) y le damos 10 segundos para responder
                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                var location = await Geolocation.GetLocationAsync(request);

                if (location != null)
                {
                    return (location.Latitude, location.Longitude);
                }
            }
            catch (FeatureNotSupportedException)
            {
                Console.WriteLine("[Error GPS] El dispositivo no tiene antena GPS.");
            }
            catch (FeatureNotEnabledException)
            {
                Console.WriteLine("[Error GPS] El GPS del celular está apagado.");
            }
            catch (PermissionException)
            {
                Console.WriteLine("[Error GPS] El usuario no dio permisos de ubicación.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error GPS] Falló la lectura: {ex.Message}");
            }

            return null; // Si algo falla, regresamos nulo
        }
    }
}