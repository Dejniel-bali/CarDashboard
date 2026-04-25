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
        static async Task Main(string[] args)
        {
            Car car = new Car();
            Random rnd = new Random();
            int tickCount = 0;
            int exitTimer = 0;
            using HttpClient client = new HttpClient();

            // Port uprav podle svého (např. 7285)
            string serverUrl = "https://localhost:7285/api/telemetry";

            Console.CursorVisible = false;
            Console.Clear();

            while (car.Fuel > 0 || car.IsPitStop)
            {
                // --- LOGIKA BEZPEČNOSTI ---
                bool isCritical = car.OilLevel < 5.0 || car.CoolantLevel < 5.0 || car.TirePressure < 1.8 || car.Fuel < 2.0;
                bool isWarning = car.OilLevel < 20.0 || car.CoolantLevel < 20.0 || car.TirePressure < 2.0 || car.Fuel < 10.0;

                if (isCritical && !car.IsPitStop)
                {
                    if (car.CurrentSpeed > 95 && !car.IsEmergencyExiting)
                    {
                        car.IsEmergencyExiting = true;
                        car.TargetSpeed = 90;
                        exitTimer = 5;
                    }
                    else if (car.IsEmergencyExiting)
                    {
                        if (car.CurrentSpeed <= 91)
                        {
                            if (exitTimer > 0) exitTimer--;
                            else { car.IsEmergencyExiting = false; car.IsPitStop = true; car.TargetSpeed = 0; }
                        }
                    }
                    else if (!car.IsEmergencyExiting)
                    {
                        car.IsPitStop = true;
                        car.TargetSpeed = 0;
                    }
                }

                // --- LOGIKA JÍZDY ---
                if (!car.IsPitStop && !car.IsEmergencyExiting)
                {
                    if (tickCount % 10 == 0)
                    {
                        int randomEvent = rnd.Next(100);
                        if (car.TargetSpeed == 0) car.TargetSpeed = 50;
                        else if (car.TargetSpeed == 130) car.TargetSpeed = randomEvent < 30 ? 90 : 130;
                        else car.TargetSpeed = rnd.Next(3) == 0 ? 50 : (rnd.Next(2) == 0 ? 90 : 130);
                    }
                }

                car.Update(1.0);

                // --- PŘEHLEDNÝ VÝPIS DO CMD ---
                VypisDashboard(car, isWarning, isCritical);

                // Odesílání na server
                await OdesliData(client, serverUrl, car);

                Thread.Sleep(1000);
                tickCount++;
            }
        }

        static void VypisDashboard(Car car, bool warning, bool critical)
        {
            Console.SetCursorPosition(0, 0);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("====================================================");
            Console.WriteLine("             DIGITÁLNÍ PALUBNÍ POČÍTAČ              ");
            Console.WriteLine("====================================================");
            Console.ResetColor();

            // Status řádek
            Console.Write(" STAV SYSTÉMU: ");
            if (car.IsPitStop) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[!] SERVISNÍ ZASTÁVKA       "); }
            else if (car.IsEmergencyExiting) { Console.ForegroundColor = ConsoleColor.DarkYellow; Console.WriteLine("[?] OPOUŠTĚNÍ DÁLNICE...    "); }
            else if (critical) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[!!!] KRITICKÁ PORUCHA      "); }
            else if (warning) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("[!] VAROVÁNÍ: NÍZKÉ HLADINY "); }
            else { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("[OK] PROVOZ NORMÁLNÍ        "); }
            Console.ResetColor();

            Console.WriteLine("----------------------------------------------------");

            // Jízda
            Console.WriteLine($" Aktuální rychlost:  {Math.Round(car.CurrentSpeed),3} km/h  ");
            Console.WriteLine($" Požadovaná rychlost:{car.TargetSpeed,3} km/h  ");
            Console.WriteLine($" Ujetá vzdálenost:   {car.Distance,6:F2} km    ");

            Console.WriteLine("----------------------------------------------------");

            // Kapaliny a pneu
            VypisHodnotu("Palivo", car.Fuel, "l", 10, 2);
            VypisHodnotu("Olej", car.OilLevel, "%", 20, 5);
            VypisHodnotu("Chlazení", car.CoolantLevel, "%", 20, 5);
            VypisHodnotu("Tlak pneu", car.TirePressure, "bar", 2.0, 1.8);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("====================================================");
            Console.ResetColor();
        }

        static void VypisHodnotu(string label, double hodnota, string jednotka, double limitWarning, double limitDanger)
        {
            Console.Write($" {label,-12}: {hodnota,6:F2} {jednotka,-4} ");
            if (hodnota < limitDanger) { Console.ForegroundColor = ConsoleColor.Red; Console.Write("[KRITICKÉ]"); }
            else if (hodnota < limitWarning) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write("[NÍZKÉ]"); }
            else { Console.ForegroundColor = ConsoleColor.Green; Console.Write("[V POŘÁDKU]"); }
            Console.WriteLine("    "); // Pročištění zbytků textu
            Console.ResetColor();
        }

        static async Task OdesliData(HttpClient client, string url, Car car)
        {
            try
            {
                var avg = car.Distance > 0.1 ? Math.Round(((50.0 - car.Fuel) / car.Distance) * 100.0, 2) : 0;
                var data = new
                {
                    Timestamp = DateTime.UtcNow,
                    CurrentSpeed = Math.Round(car.CurrentSpeed, 2),
                    TargetSpeed = car.TargetSpeed,
                    Distance = Math.Round(car.Distance, 2),
                    FuelRemaining = Math.Round(car.Fuel, 2),
                    AverageConsumption = avg,
                    OilLevel = Math.Round(car.OilLevel, 2),
                    CoolantLevel = Math.Round(car.CoolantLevel, 2),
                    TirePressure = Math.Round(car.TirePressure, 2)
                };
                await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json"));
            }
            catch { /* Server offline - simulace běží dál */ }
        }
    }

    class Car
    {
        public double CurrentSpeed { get; set; } = 0;
        public double TargetSpeed { get; set; } = 0;
        public double Distance { get; set; } = 0;
        public double Fuel { get; set; } = 50.0;
        public double OilLevel { get; set; } = 28.0;
        public double CoolantLevel { get; set; } = 25.0;
        public double TirePressure { get; set; } = 2.3;
        public bool IsPitStop { get; set; } = false;
        public bool IsEmergencyExiting { get; set; } = false;

        public void Update(double dt)
        {
            double power = IsEmergencyExiting ? 5.0 : 20.0;

            if (CurrentSpeed < TargetSpeed) CurrentSpeed = Math.Min(TargetSpeed, CurrentSpeed + 12.0 * dt);
            else if (CurrentSpeed > TargetSpeed) CurrentSpeed = Math.Max(TargetSpeed, CurrentSpeed - power * dt);

            Distance += (CurrentSpeed / 3600.0) * dt;

            if (IsPitStop && CurrentSpeed < 0.1)
            {
                Fuel += 15 * dt; OilLevel += 25 * dt; CoolantLevel += 25 * dt; TirePressure += 0.3 * dt;
                if (Fuel >= 50 && OilLevel >= 100 && CoolantLevel >= 100 && TirePressure >= 2.5)
                {
                    Fuel = 50; OilLevel = 100; CoolantLevel = 100; TirePressure = 2.5;
                    IsPitStop = false; TargetSpeed = 50;
                }
            }
            else
            {
                Fuel -= (0.001 + (CurrentSpeed * CurrentSpeed * 0.0000003)) * dt;
                OilLevel -= 0.12 * dt; CoolantLevel -= 0.14 * dt; TirePressure -= 0.004 * dt;
            }
        }
    }
}