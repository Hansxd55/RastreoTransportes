using MySqlConnector;
using Microsoft.AspNetCore.SignalR; 

var builder = WebApplication.CreateBuilder(args);

// 1. Encendemos el servicio de WebSockets (SignalR)
builder.Services.AddSignalR();

// Habilitar CORS
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

// --- ENDPOINT PARA EL CHOFER (GUARDA LA UBICACIÓN) ---
app.MapPost("/api/coordenadas", async (UbicacionChofer ubicacion, IHubContext<RastreoHub> hubContext) =>
{
    // 1. Imprimimos en consola para confirmar que la laptop recibió el dato
    Console.WriteLine($"[SIGNALR ENVIANDO] Unidad: {ubicacion.Unidad} | Lat: {ubicacion.Latitud} | Lon: {ubicacion.Longitud}");

    // 2. Rebotamos la coordenada INMEDIATAMENTE a la app del pasajero
    await hubContext.Clients.All.SendAsync("RecibirUbicacion", ubicacion);

    return Results.Ok();
});

// --- NUEVO: ENDPOINT PARA EL PASAJERO (LEE LA UBICACIÓN) ---
app.MapGet("/api/coordenadas", async () =>
{
    try
    {
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        // Buscamos la última coordenada registrada ordenando por la fecha más reciente
        using var command = new MySqlCommand(
            "SELECT latitud, longitud FROM Coordenadas_GPS ORDER BY timestamp DESC LIMIT 1;", 
            connection);
        
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            // Devolvemos el JSON exacto que Avalonia está esperando
            return Results.Ok(new { 
                latitud = reader.GetDouble("latitud"), 
                longitud = reader.GetDouble("longitud") 
            });
        }
        
        return Results.NotFound();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// Dejamos tu endpoint GET intacto por si acaso
app.MapGet("/api/coordenadas/ultima/{idUnidad}", async (int idUnidad) =>
{
    return Results.Ok();
});

// Mapeamos la ruta del túnel de WebSockets
app.MapHub<RastreoHub>("/rastreohub");

app.Run();

// --- CLASES Y MODELOS ---
public class RastreoHub : Hub { }

public class UbicacionChofer
{
    public string? Unidad { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
}