using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// 1. Zaregistrování databázového kontextu do aplikace
builder.Services.AddDbContext<AppDbContext>();

var app = builder.Build();

// 2. Automatické vytvoření databáze při spuštění serveru (pokud ještě neexistuje)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// 3. Endpoint přijímá data a navíc má nyní přístup k databázi (AppDbContext db)
app.MapPost("/api/telemetry", async (TelemetryData data, AppDbContext db) =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[SERVER PŘIJAL {data.Timestamp:HH:mm:ss}] - Ukládám do DB...");
    Console.ResetColor();

    Console.WriteLine($"  -> Rychlost: {data.CurrentSpeed} km/h (Cíl: {data.TargetSpeed})");
    Console.WriteLine($"  -> Palivo:   {data.FuelRemaining} l");

    // Tady se děje to kouzlo: Přidáme přijatá data do tabulky a uložíme
    db.TelemetryRecords.Add(data);
    await db.SaveChangesAsync();

    return Results.Ok();
});

app.Run();


// --- MODELY A DATABÁZOVÁ KONFIGURACE ---

// Třída reprezentující samotnou databázi
public class AppDbContext : DbContext
{
    // Toto je naše tabulka v databázi, do které se budou ukládat záznamy
    public DbSet<TelemetryData> TelemetryRecords { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Říkáme aplikaci, aby použila SQLite a uložila ho do souboru "telemetry.db"
        optionsBuilder.UseSqlite("Data Source=telemetry.db");
    }
}

// Třída dat obohacená o Id
public class TelemetryData
{
    public int Id { get; set; } // Primární klíč - databáze si bude číslovat řádky sama (1, 2, 3...)
    public DateTime Timestamp { get; set; }
    public double CurrentSpeed { get; set; }
    public double TargetSpeed { get; set; }
    public double Distance { get; set; }
    public double FuelRemaining { get; set; }
    public double AverageConsumption { get; set; }
}