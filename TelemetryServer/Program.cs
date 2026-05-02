using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Nastavení databáze
builder.Services.AddDbContext<TelemetryDb>(options =>
    options.UseSqlite("Data Source=telemetry.db"));

builder.Services.AddCors();

var app = builder.Build();

app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

// TYTO DVA ŘÁDKY JSOU NUTNÉ PRO ZOBRAZENÍ WEBU (index.html, admin.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// Vytvoření databáze při startu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TelemetryDb>();
    db.Database.EnsureCreated();
}

// --- PŘIJÍMAČ PŘÍKAZŮ Z ADMIN WEBU ---
app.MapPost("/api/command", (SimCommand cmd) =>
{
    CommandStore.CurrentCommand = cmd;
    return Results.Ok();
});

app.MapGet("/api/command", () =>
{
    var cmd = CommandStore.CurrentCommand;
    CommandStore.CurrentCommand = new SimCommand(); // Vymaže paměť po přečtení autem
    return Results.Ok(cmd);
});

// --- PŘÍJEM A ODESÍLÁNÍ TELEMETRIE ---
app.MapPost("/api/telemetry", async (TelemetryData data, TelemetryDb db) =>
{
    db.Telemetry.Add(data);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/api/telemetry/latest", async (TelemetryDb db) =>
{
    var latest = await db.Telemetry.OrderByDescending(t => t.Timestamp).FirstOrDefaultAsync();
    return latest != null ? Results.Ok(latest) : Results.NotFound();
});

app.MapGet("/api/telemetry/history", async (TelemetryDb db) =>
{
    var history = await db.Telemetry.OrderByDescending(t => t.Timestamp).Take(50).ToListAsync();
    history.Reverse(); // Otočení pro správné vykreslení zleva doprava v grafu
    return Results.Ok(history);
});

app.Run();

// ==========================================
// --- DATOVÉ MODELY ---
// ==========================================

public class TelemetryDb : DbContext
{
    public TelemetryDb(DbContextOptions<TelemetryDb> options) : base(options) { }
    public DbSet<TelemetryData> Telemetry => Set<TelemetryData>();
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
    public double CurrentConsumption { get; set; }
    public double OilLevel { get; set; }
    public double CoolantTemp { get; set; }
    public double TirePressure { get; set; }
    public double EstimatedRange { get; set; }
    public bool LightsOn { get; set; }
    public double WasherFluidLevel { get; set; }
    public bool IsDefected { get; set; }
    public double MaxFuelCapacity { get; set; }
    public double TargetDistance { get; set; }
    public int SimSpeedMultiplier { get; set; }
}

public class SimCommand
{
    public string Action { get; set; } = "";
    public double TargetDistance { get; set; } = 50.0;
    public string CarType { get; set; } = "Sedan";
}

public static class CommandStore
{
    public static SimCommand CurrentCommand { get; set; } = new SimCommand();
}