using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>();

var app = builder.Build();

// 1. TOTO JE NOVÉ: Povolíme serveru zobrazovat webové stránky (HTML, CSS, JS)
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Původní endpoint pro ZÁPIS dat ze simulátoru
app.MapPost("/api/telemetry", async (TelemetryData data, AppDbContext db) =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[SERVER PŘIJAL {data.Timestamp:HH:mm:ss}] - Ukládám do DB...");
    Console.ResetColor();

    db.TelemetryRecords.Add(data);
    await db.SaveChangesAsync();
    return Results.Ok();
});

// 2. NOVÝ ENDPOINT: Vrátí úplně ten nejnovější záznam (pro budíky)
app.MapGet("/api/telemetry/latest", (AppDbContext db) =>
{
    var latest = db.TelemetryRecords.OrderByDescending(t => t.Id).FirstOrDefault();
    return latest is not null ? Results.Ok(latest) : Results.NotFound();
});

// 3. NOVÝ ENDPOINT: Vrátí posledních 30 záznamů (pro vykreslení grafu)
app.MapGet("/api/telemetry/history", (AppDbContext db) =>
{
    var history = db.TelemetryRecords.OrderByDescending(t => t.Id).Take(30).ToList();
    history.Reverse(); // Chceme je od nejstaršího po nejnovější zleva doprava
    return Results.Ok(history);
});

app.Run();

// Modely zůstávají stejné...
public class AppDbContext : DbContext
{
    public DbSet<TelemetryData> TelemetryRecords { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=telemetry.db");
    }
}

public class TelemetryData
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public double CurrentSpeed { get; set; }
    public double TargetSpeed { get; set; }
    public double Distance { get; set; }
    public double FuelRemaining { get; set; }
    public double AverageConsumption { get; set; }
    public double OilLevel { get; set; }
    public double CoolantLevel { get; set; }
    public double TirePressure { get; set; }
}