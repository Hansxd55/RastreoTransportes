using MySqlConnector;
using Microsoft.AspNetCore.SignalR; // Importamos SignalR

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

// 1. Encendemos el servicio de WebSockets (SignalR)
builder.Services.AddSignalR();

// Habilitar CORS por si luego conectas un cliente web en otro puerto
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed((host) => true)
              .AllowCredentials();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var app = builder.Build();

app.UseCors("PermitirTodo");

// 2. Inyectamos el IHubContext aquí para poder transmitir
app.MapPost("/api/coordenadas", async (UbicacionChofer ubicacion, IHubContext<RastreoHub> hubContext) =>
{
    Console.WriteLine($"[GUARDANDO EN BD] Unidad: {ubicacion.Unidad} | Lat: {ubicacion.Latitud} | Lon: {ubicacion.Longitud}");

    try
    {
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new MySqlCommand(
            "INSERT INTO Coordenadas_GPS (id_unidad, latitud, longitud, velocidad, timestamp) VALUES (@id_unidad, @lat, @lon, 0, NOW());", 
            connection);
        
        command.Parameters.AddWithValue("@id_unidad", 1); 
        command.Parameters.AddWithValue("@lat", ubicacion.Latitud);
        command.Parameters.AddWithValue("@lon", ubicacion.Longitud);

        await command.ExecuteNonQueryAsync();

        // 3. ¡LA MAGIA EN TIEMPO REAL! 
        // Avisamos a todos los clientes conectados al Hub que hay nuevas coordenadas
        await hubContext.Clients.All.SendAsync("RecibirUbicacion", ubicacion);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

    return Results.Ok();
});

// Dejamos tu endpoint GET intacto por si acaso
app.MapGet("/api/coordenadas/ultima/{idUnidad}", async (int idUnidad) =>
{
    // ... (aquí se queda el mismo código que ya tenías para el GET)
    return Results.Ok();
});

// 4. Mapeamos la ruta del túnel de WebSockets
app.MapHub<RastreoHub>("/rastreohub");

app.Run();


// --- CLASES Y MODELOS ---

// El "Hub" o canal de comunicación. 
public class RastreoHub : Hub
{
    // Aquí podrías agregar lógica en el futuro, como cuando un usuario se conecta o desconecta.
}

public class UbicacionChofer
{
    public string? Unidad { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
}