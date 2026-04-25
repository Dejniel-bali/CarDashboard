using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Tento "endpoint" čeká na data ze simulátoru
app.MapPost("/api/telemetry", (TelemetryData data) =>
{
    // Změníme barvu konzole pro lepší přehlednost
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[SERVER PŘIJAL {data.Timestamp:HH:mm:ss}]");
    Console.ResetColor();

    // Vypíšeme přijatá data
    Console.WriteLine($"  -> Rychlost: {data.CurrentSpeed} km/h (Cíl: {data.TargetSpeed})");
    Console.WriteLine($"  -> Ujeto:    {data.Distance} km");
    Console.WriteLine($"  -> Palivo:   {data.FuelRemaining} l (Spotřeba: {data.AverageConsumption} l/100km)\n");

    // Odpovíme simulátoru, že vše proběhlo v pořádku (HTTP 200 OK)
    return Results.Ok();
});

// Spuštění serveru
app.Run();

// Třída, která přesně kopíruje strukturu JSONu, který posíláme ze simulátoru
public class TelemetryData
{
    public DateTime Timestamp { get; set; }
    public double CurrentSpeed { get; set; }
    public double TargetSpeed { get; set; }
    public double Distance { get; set; }
    public double FuelRemaining { get; set; }
    public double AverageConsumption { get; set; }
}