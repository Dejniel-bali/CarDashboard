using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<TelemetryDb>(options => options.UseSqlite("Data Source=telemetry.db"));
builder.Services.AddCors();
var app = builder.Build();
app.UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TelemetryDb>();
    db.Database.EnsureCreated();
}

// --- API ---
app.MapPost("/api/command", (SimCommand cmd) => { CommandStore.CurrentCommand = cmd; return Results.Ok(); });
app.MapGet("/api/command", () => {
    var cmd = CommandStore.CurrentCommand;
    CommandStore.CurrentCommand = new SimCommand { Action = "" }; // Reset akce
    return Results.Ok(cmd);
});

app.MapPost("/api/telemetry", async (TelemetryData data, TelemetryDb db) => {
    db.Telemetry.Add(data);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/api/telemetry/latest", async (TelemetryDb db) => {
    var latest = await db.Telemetry.OrderByDescending(t => t.Timestamp).FirstOrDefaultAsync();
    return latest != null ? Results.Ok(latest) : Results.NotFound();
});

// Získání seznamu unikátních jízd pro historii
app.MapGet("/api/history/trips", async (TelemetryDb db) => {
    var trips = await db.Telemetry
        .GroupBy(t => t.TripId)
        .Select(g => new {
            TripId = g.Key,
            Start = g.Min(t => t.Timestamp),
            Distance = g.Max(t => t.Distance),
            CarType = g.First().CarType
        })
        .OrderByDescending(x => x.Start)
        .ToListAsync();
    return Results.Ok(trips);
});

// Získání dat konkrétní jízdy
app.MapGet("/api/history/trip/{id}", async (string id, TelemetryDb db) => {
    var data = await db.Telemetry.Where(t => t.TripId == id).OrderBy(t => t.Timestamp).ToListAsync();
    return Results.Ok(data);
});

app.MapGet("/api/telemetry/history", async (TelemetryDb db) => {
    var latestTrip = await db.Telemetry.OrderByDescending(t => t.Timestamp).Select(t => t.TripId).FirstOrDefaultAsync();
    var history = await db.Telemetry.Where(t => t.TripId == latestTrip).OrderByDescending(t => t.Timestamp).Take(50).ToListAsync();
    history.Reverse();
    return Results.Ok(history);
});
// --- MAZÁNÍ KONKRÉTNÍ JÍZDY ---
app.MapDelete("/api/history/trip/{id}", async (string id, TelemetryDb db) => {
    var tripData = await db.Telemetry.Where(t => t.TripId == id).ToListAsync();
    if (tripData.Any())
    {
        db.Telemetry.RemoveRange(tripData);
        await db.SaveChangesAsync();
        return Results.Ok();
    }
    return Results.NotFound();
});
app.Run();

public class TelemetryDb : DbContext
{
    public TelemetryDb(DbContextOptions<TelemetryDb> options) : base(options) { }
    public DbSet<TelemetryData> Telemetry => Set<TelemetryData>();
}

public class TelemetryData
{
    public int Id { get; set; }
    public string TripId { get; set; } = ""; // Unikátní ID jízdy
    public string CarType { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public double CurrentSpeed { get; set; }
    public double Distance { get; set; }
    public double FuelRemaining { get; set; }
    public double CurrentConsumption { get; set; }
    public double AverageConsumption { get; set; }
    public double OilLevel { get; set; }
    public double CoolantTemp { get; set; }
    public double EstimatedRange { get; set; }
    public bool LightsOn { get; set; }
    public double WasherFluidLevel { get; set; }
    public bool IsDefected { get; set; }
    public double MaxFuelCapacity { get; set; }
    public double TargetDistance { get; set; }
    public int SimSpeedMultiplier { get; set; }
    // Individuální pneumatiky
    public double TireFL { get; set; }
    public double TireFR { get; set; }
    public double TireRL { get; set; }
    public double TireRR { get; set; }
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