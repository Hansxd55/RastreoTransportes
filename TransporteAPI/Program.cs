using MySqlConnector; // Importamos el conector

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

// Extraemos la cadena de conexión del appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var app = builder.Build();

app.MapPost("/api/coordenadas", async (UbicacionChofer ubicacion) =>
{
    Console.WriteLine($"[GUARDANDO EN BD] Unidad: {ubicacion.Unidad} | Lat: {ubicacion.Latitud} | Lon: {ubicacion.Longitud}");

    try
    {
        // Abrimos la conexión a MySQL
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

      // Preparamos la consulta con TUS columnas exactas
        using var command = new MySqlCommand(
            "INSERT INTO Coordenadas_GPS (id_unidad, latitud, longitud, velocidad, timestamp) VALUES (@id_unidad, @lat, @lon, 0, NOW());", 
            connection);
        
        // El AppChofer manda "Unidad-01", pero la BD necesita un número por la llave foránea. 
        // Usamos el ID 1 temporalmente.
        command.Parameters.AddWithValue("@id_unidad", 1); 
        command.Parameters.AddWithValue("@lat", ubicacion.Latitud);
        command.Parameters.AddWithValue("@lon", ubicacion.Longitud);

        // Ejecutamos el insert
        await command.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al guardar en BD: {ex.Message}");
    }

    return Results.Ok();
});

app.Run();

public class UbicacionChofer
{
    public string? Unidad { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
}