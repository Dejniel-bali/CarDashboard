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

            string serverUrl = "https://localhost:7285/api/telemetry"; // Zkontroluj port!

            Console.CursorVisible = false;
            Console.Clear();

            while (car.Distance < 50 && (car.Fuel > 0 || car.IsPitStop || car.IsDefected))
            {
                // Defekt
                if (!car.IsDefected && !car.IsPitStop && car.CurrentSpeed > 10 && rnd.Next(100) < 1)
                {
                    car.IsDefected = true;
                    car.RepairTimer = 10;
                    car.TargetSpeed = 0;
                }

                if (car.IsDefected && car.CurrentSpeed < 0.5)
                {
                    car.RepairTimer--;
                    if (car.RepairTimer <= 0) { car.IsDefected = false; car.TargetSpeed = 50; }
                }

                // Ostřikovače
                if (!car.IsPitStop && !car.IsDefected && rnd.Next(100) < 15)
                {
                    car.WasherFluidLevel -= 1.5;
                    if (car.WasherFluidLevel < 0) car.WasherFluidLevel = 0;
                }

                // --- LOGIKA BEZPEČNOSTI (Upraveno pro teplotu) ---
                bool isCritical = car.OilLevel < 5.0 || car.CoolantTemp >= 120.0 || car.TirePressure < 1.8 || car.Fuel < 2.0;
                bool isWarning = car.OilLevel < 20.0 || car.CoolantTemp >= 110.0 || car.TirePressure < 2.0 || car.Fuel < 10.0 || car.WasherFluidLevel < 20.0;

                if (isCritical && !car.IsPitStop && !car.IsDefected)
                {
                    if (car.CurrentSpeed > 95 && !car.IsEmergencyExiting)
                    {
                        car.IsEmergencyExiting = true; car.TargetSpeed = 90; exitTimer = 5;
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
                        car.IsPitStop = true; car.TargetSpeed = 0;
                    }
                }

                // Logika jízdy
                if (!car.IsPitStop && !car.IsEmergencyExiting && !car.IsDefected)
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

                double avgCons = car.GetAverageConsumption();
                double range = avgCons > 0 ? (car.Fuel / avgCons) * 100 : 0;

                VypisDashboard(car, isWarning, isCritical, range);
                await OdesliData(client, serverUrl, car, range);

                Thread.Sleep(1000);
                tickCount++;
            }
        }

        static void VypisDashboard(Car car, bool warning, bool critical, double range)
        {
            Console.SetCursorPosition(0, 0);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("====================================================");
            Console.WriteLine("             DIGITÁLNÍ PALUBNÍ POČÍTAČ              ");
            Console.WriteLine("====================================================");
            Console.ResetColor();

            Console.Write(" STAV SYSTÉMU: ");
            if (car.IsDefected) { Console.ForegroundColor = ConsoleColor.Magenta; Console.WriteLine($"[💥] DEFEKT VOZIDLA! Oprava: {car.RepairTimer}s  "); }
            else if (car.IsPitStop) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[!] SERVISNÍ ZASTÁVKA       "); }
            else if (car.IsEmergencyExiting) { Console.ForegroundColor = ConsoleColor.DarkYellow; Console.WriteLine("[?] OPOUŠTĚNÍ DÁLNICE...    "); }
            else if (critical) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[!!!] KRITICKÁ PORUCHA      "); }
            else if (warning) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("[!] VAROVÁNÍ: ZKONTROLUJTE VŮZ"); }
            else { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("[OK] PROVOZ NORMÁLNÍ        "); }
            Console.ResetColor();

            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine($" Rychlost:  {Math.Round(car.CurrentSpeed),3} km/h (Cíl: {car.TargetSpeed,3}) | Světla: {(car.LightsOn ? "ZAPNUTO " : "VYPNUTO ")}");
            Console.WriteLine($" Ujeto:     {car.Distance,5:F2} / 50.00 km | Dojezd: {Math.Round(range),4} km  ");
            Console.WriteLine($" Spotřeba:  {car.CurrentConsumptionPer100km,4:F1} l/100km (Průměr: {car.GetAverageConsumption():F1})      ");
            Console.WriteLine("----------------------------------------------------");

            // Výpis hodnot (poslední boolean parametr určuje, zda je vyšší číslo horší)
            VypisHodnotu("Palivo", car.Fuel, "l", 10, 2, false);
            VypisHodnotu("Olej", car.OilLevel, "%", 20, 5, false);
            VypisHodnotu("Teplota", car.CoolantTemp, "°C", 110, 120, true); // NOVÉ
            VypisHodnotu("Tlak pneu", car.TirePressure, "bar", 2.0, 1.8, false);
            VypisHodnotu("Ostřikovače", car.WasherFluidLevel, "%", 20, 5, false);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("====================================================");
            Console.ResetColor();
        }

        static void VypisHodnotu(string label, double hodnota, string jednotka, double limitWarning, double limitDanger, bool higherIsWorse)
        {
            Console.Write($" {label,-12}: {hodnota,6:F2} {jednotka,-4} ");

            bool isDanger = higherIsWorse ? hodnota >= limitDanger : hodnota < limitDanger;
            bool isWarning = higherIsWorse ? hodnota >= limitWarning : hodnota < limitWarning;

            if (isDanger) { Console.ForegroundColor = ConsoleColor.Red; Console.Write("[KRITICKÉ] "); }
            else if (isWarning) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write("[VAROVÁNÍ] "); }
            else { Console.ForegroundColor = ConsoleColor.Green; Console.Write("[V POŘÁDKU]"); }
            Console.WriteLine("    ");
            Console.ResetColor();
        }

        static async Task OdesliData(HttpClient client, string url, Car car, double range)
        {
            try
            {
                var data = new
                {
                    Timestamp = DateTime.UtcNow,
                    CurrentSpeed = Math.Round(car.CurrentSpeed, 2),
                    TargetSpeed = car.TargetSpeed,
                    Distance = Math.Round(car.Distance, 2),
                    FuelRemaining = Math.Round(car.Fuel, 2),
                    AverageConsumption = Math.Round(car.GetAverageConsumption(), 2),
                    CurrentConsumption = Math.Round(car.CurrentConsumptionPer100km, 2),
                    OilLevel = Math.Round(car.OilLevel, 2),
                    CoolantTemp = Math.Round(car.CoolantTemp, 1), // NOVÉ
                    TirePressure = Math.Round(car.TirePressure, 2),
                    EstimatedRange = Math.Round(range, 1),
                    LightsOn = car.LightsOn,
                    WasherFluidLevel = Math.Round(car.WasherFluidLevel, 2),
                    IsDefected = car.IsDefected
                };
                await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json"));
            }
            catch { }
        }
    }

    class Car
    {
        public double CurrentSpeed { get; set; } = 0;
        public double TargetSpeed { get; set; } = 0;
        public double Distance { get; set; } = 0;

        public double Fuel { get; set; } = 50.0;
        public double TotalFuelConsumed { get; private set; } = 0;
        public double CurrentConsumptionPer100km { get; private set; } = 0;

        public double OilLevel { get; set; } = 28.0;
        public double CoolantTemp { get; set; } = 85.0; // Začínáme na 85 °C
        public double TirePressure { get; set; } = 2.3;
        public double WasherFluidLevel { get; set; } = 100.0;

        public bool IsPitStop { get; set; } = false;
        public bool IsEmergencyExiting { get; set; } = false;
        public bool IsDefected { get; set; } = false;
        public int RepairTimer { get; set; } = 0;

        public bool LightsOn => CurrentSpeed > 0.5 || TargetSpeed > 0;

        public double GetAverageConsumption() => Distance > 0.1 ? (TotalFuelConsumed / Distance) * 100.0 : 0;

        public void Update(double dt)
        {
            if (IsPitStop && CurrentSpeed < 0.1)
            {
                bool isReady = true;
                if (Fuel < 10) { Fuel += 15 * dt; isReady = false; }
                if (OilLevel < 20) { OilLevel += 25 * dt; isReady = false; }
                if (TirePressure < 2.0) { TirePressure += 0.3 * dt; isReady = false; }
                if (WasherFluidLevel < 20) { WasherFluidLevel += 25 * dt; isReady = false; }

                // Zchlazení motoru
                if (CoolantTemp > 85.0) { CoolantTemp -= 8.0 * dt; isReady = false; }

                if (Fuel > 50) Fuel = 50;
                if (OilLevel > 100) OilLevel = 100;
                if (TirePressure > 2.5) TirePressure = 2.5;
                if (WasherFluidLevel > 100) WasherFluidLevel = 100;
                if (CoolantTemp < 85.0) CoolantTemp = 85.0;

                CurrentConsumptionPer100km = 0;

                if (isReady) { IsPitStop = false; TargetSpeed = 50; }
            }
            else
            {
                double power = IsEmergencyExiting ? 5.0 : (IsDefected ? 25.0 : 20.0);

                if (CurrentSpeed < TargetSpeed) CurrentSpeed = Math.Min(TargetSpeed, CurrentSpeed + 12.0 * dt);
                else if (CurrentSpeed > TargetSpeed) CurrentSpeed = Math.Max(TargetSpeed, CurrentSpeed - power * dt);

                Distance += (CurrentSpeed / 3600.0) * dt;

                if (CurrentSpeed < 1.0)
                {
                    Fuel -= (0.8 / 3600.0) * dt;
                    TotalFuelConsumed += (0.8 / 3600.0) * dt;
                    CurrentConsumptionPer100km = 0;

                    // Na volnoběh teplota lehce klesá k 90°C
                    if (CoolantTemp > 90) CoolantTemp -= 0.5 * dt;
                }
                else
                {
                    double speedFactor = CurrentSpeed / 100.0;
                    double steadyCons = 3.5 + (speedFactor * speedFactor * 3.5);

                    if (CurrentSpeed < TargetSpeed) steadyCons += 18.0;
                    else if (CurrentSpeed > TargetSpeed) steadyCons = 0.0;

                    CurrentConsumptionPer100km = steadyCons;
                    double litersPerSec = (steadyCons / 100.0) * (CurrentSpeed / 3600.0);

                    Fuel -= litersPerSec * dt;
                    TotalFuelConsumed += litersPerSec * dt;

                    // REALISTICKÁ TEPLOTA: Roste mírně sama o sobě (simulace úniku chlazení), plus reaguje na zátěž
                    CoolantTemp += (0.15 + (CurrentConsumptionPer100km / 150.0)) * dt;
                }

                OilLevel -= 0.12 * dt; TirePressure -= 0.004 * dt;
            }
        }
    }
}