using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarSimulator
{
    class Program
    {
        // Metoda Main je nyní asynchronní, aby mohla čekat na odpověď serveru
        static async Task Main(string[] args)
        {
            Car car = new Car();
            Random rnd = new Random();
            int tickCount = 0;

            // 1. PŘÍPRAVA PRO KOMUNIKACI SE SERVEREM
            using HttpClient client = new HttpClient();
            // TOTO JE DŮLEŽITÉ: Zde musí být adresa tvého budoucího serveru (API). 
            // Nyní je tam nastavena lokální testovací adresa.
            string serverUrl = "https://localhost:7285/api/telemetry";

            Console.CursorVisible = false;
            Console.Clear();
            Console.WriteLine("=== SIMULACE JÍZDY AUTA A ODESÍLÁNÍ NA SERVER ===");
            Console.WriteLine("(Stiskněte CTRL+C pro ukončení)\n");

            while (car.Fuel > 0)
            {
                // Náhodné události
                if (tickCount % 10 == 0)
                {
                    int randomEvent = rnd.Next(100);
                    if (randomEvent < 15) car.TargetSpeed = 0;
                    else if (randomEvent < 45) car.TargetSpeed = 50;
                    else if (randomEvent < 75) car.TargetSpeed = 90;
                    else car.TargetSpeed = 130;
                }

                // Aktualizace fyziky
                car.Update(1.0);

                // Vykreslení do konzole
                Console.SetCursorPosition(0, 3);
                Console.WriteLine($"Cílová rychlost:   {car.TargetSpeed,5} km/h   ");
                Console.WriteLine($"Aktuální rychlost: {Math.Round(car.CurrentSpeed),5} km/h   ");
                Console.WriteLine($"Ujetá vzdálenost:  {car.Distance,8:F2} km    ");
                Console.WriteLine($"Zbývající palivo:  {car.Fuel,8:F2} l     ");

                double avgConsumption = 0;
                if (car.Distance > 0.1)
                {
                    avgConsumption = ((50.0 - car.Fuel) / car.Distance) * 100.0;
                    Console.WriteLine($"Průměrná spotřeba: {avgConsumption,8:F2} l/100km   ");
                }
                else
                {
                    Console.WriteLine($"Průměrná spotřeba:   Počítám...         ");
                }

                // --- 2. ODESLÁNÍ DAT NA SERVER ---
                // Data neposíláme každou vteřinu, abychom nezahltili server, ale třeba každé 2 vteřiny
                if (tickCount % 2 == 0)
                {
                    // Vytvoříme anonymní objekt s daty, která chceme poslat
                    var telemetryData = new
                    {
                        Timestamp = DateTime.UtcNow,
                        CurrentSpeed = Math.Round(car.CurrentSpeed, 2),
                        TargetSpeed = car.TargetSpeed,
                        Distance = Math.Round(car.Distance, 2),
                        FuelRemaining = Math.Round(car.Fuel, 2),
                        AverageConsumption = Math.Round(avgConsumption, 2)
                    };

                    try
                    {
                        // Převedeme objekt na JSON text
                        string jsonString = JsonSerializer.Serialize(telemetryData);
                        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                        // Odešleme na server metodou POST
                        HttpResponseMessage response = await client.PostAsync(serverUrl, content);

                        Console.SetCursorPosition(0, 10);
                        if (response.IsSuccessStatusCode)
                            Console.WriteLine($"[Server]: Odesláno OK (Status {response.StatusCode})       ");
                        else
                            Console.WriteLine($"[Server]: Chyba API (Status {response.StatusCode})         ");
                    }
                    catch (Exception)
                    {
                        Console.SetCursorPosition(0, 10);
                        // Pokud server neexistuje nebo neběží, vyhodí to výjimku, zachytíme ji
                        Console.WriteLine($"[Server]: Nedostupný. Zkontroluj URL '{serverUrl}'           ");
                    }
                }

                Thread.Sleep(1000);
                tickCount++;
            }

            Console.WriteLine("\nDOŠLO PALIVO! Auto zastavilo. Konec simulace.");
        }
    }

    class Car
    {
        // Třída Car zůstává úplně stejná jako v předchozím kroku
        public double CurrentSpeed { get; private set; } = 0;
        public double TargetSpeed { get; set; } = 0;
        public double Distance { get; private set; } = 0;
        public double Fuel { get; private set; } = 50.0;

        public void Update(double deltaTimeSeconds)
        {
            double accelerationRate = 12.0;
            double brakingRate = 20.0;

            if (CurrentSpeed < TargetSpeed)
            {
                CurrentSpeed += accelerationRate * deltaTimeSeconds;
                if (CurrentSpeed > TargetSpeed) CurrentSpeed = TargetSpeed;
            }
            else if (CurrentSpeed > TargetSpeed)
            {
                CurrentSpeed -= brakingRate * deltaTimeSeconds;
                if (CurrentSpeed < TargetSpeed) CurrentSpeed = TargetSpeed;
            }

            double distanceThisTick = (CurrentSpeed / 3600.0) * deltaTimeSeconds;
            Distance += distanceThisTick;

            double idleConsumption = (0.8 / 3600.0) * deltaTimeSeconds;
            double speedConsumption = (CurrentSpeed * CurrentSpeed * 0.0000002) * deltaTimeSeconds;
            double accelerationConsumption = 0;

            if (CurrentSpeed < TargetSpeed)
            {
                accelerationConsumption = (0.001) * deltaTimeSeconds;
            }

            Fuel -= (idleConsumption + speedConsumption + accelerationConsumption);
            if (Fuel < 0) Fuel = 0;
        }
    }
}