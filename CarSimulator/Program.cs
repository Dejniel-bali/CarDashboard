using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarSimulator
{
    class Program
    {
        static Random rnd = new Random();

        static async Task Main(string[] args)
        {
            Car car = new Car();
            int tickCount = 0;
            using HttpClient client = new HttpClient();

            string serverUrl = "https://localhost:7285/api/telemetry";
            string commandUrl = "https://localhost:7285/api/command";

            Console.CursorVisible = false;
            Console.Clear();

            bool isRunning = false;
            bool isPaused = false;
            double targetDistance = 50.0;
            int simMultiplier = 1;
            double exitTimer = 0;

            Console.WriteLine("Čekám na instrukce z dispečinku...");

            while (true)
            {
                var cmd = await FetchCommand(client, commandUrl);

                // Zpracování příkazů ze serveru
                if (cmd != null && !string.IsNullOrEmpty(cmd.Action))
                {
                    switch (cmd.Action)
                    {
                        case "start":
                            string typeToSet = string.IsNullOrEmpty(cmd.CarType) ? "Sedan" : cmd.CarType;
                            car.ResetCar(typeToSet);
                            targetDistance = cmd.TargetDistance > 0 ? cmd.TargetDistance : 50.0;
                            isRunning = true;
                            isPaused = false;
                            simMultiplier = 1;
                            break;
                        case "pause": isPaused = !isPaused; break;
                        case "stop": isRunning = false; break;
                        case "sim_faster":
                            if (simMultiplier == 1) simMultiplier = 2;
                            else if (simMultiplier == 2) simMultiplier = 5;
                            else if (simMultiplier == 5) simMultiplier = 10;
                            break;
                        case "sim_slower":
                            if (simMultiplier == 10) simMultiplier = 5;
                            else if (simMultiplier == 5) simMultiplier = 2;
                            else if (simMultiplier == 2) simMultiplier = 1;
                            break;
                        case "defect":
                            car.IsDefected = true;
                            car.RepairTimer = 10;
                            car.TargetSpeed = 0;
                            break;
                        case "drop_oil": car.OilLevel = 4.0; break;
                        case "drop_water": car.IsOverheating = true; break;
                    }
                }

                if (!isRunning)
                {
                    await Task.Delay(1000); // Správné asynchronní čekání
                    continue;
                }

                // Hlavní simulační smyčka
                if (!isPaused && car.Distance < targetDistance && (car.Fuel > 0 || car.IsPitStop || car.IsDefected))
                {
                    if (car.IsDefected && car.CurrentSpeed < 0.5)
                    {
                        car.RepairTimer -= simMultiplier;
                        if (car.RepairTimer <= 0) { car.IsDefected = false; car.TargetSpeed = 50; }
                    }

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

                    bool isCritical = car.OilLevel < 5.0 || car.CoolantTemp >= 120.0 || car.TirePressure < 1.8 || car.Fuel < 2.0;

                    if (isCritical && !car.IsPitStop && !car.IsDefected)
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
                                if (exitTimer > 0) exitTimer -= simMultiplier;
                                else { car.IsEmergencyExiting = false; car.IsPitStop = true; car.TargetSpeed = 0; }
                            }
                        }
                        else if (!car.IsEmergencyExiting)
                        {
                            car.IsPitStop = true;
                            car.TargetSpeed = 0;
                        }
                    }

                    car.Update(1.0 * simMultiplier, rnd);
                }

                double avgCons = car.GetAverageConsumption();
                double range = avgCons > 0 ? (car.Fuel / avgCons) * 100 : 0;

                VypisDashboard(car, isPaused, targetDistance, simMultiplier);
                await OdesliData(client, serverUrl, car, range, targetDistance, simMultiplier);

                await Task.Delay(1000); // Správné asynchronní čekání
                tickCount++;
            }
        }

        static async Task<SimCommand> FetchCommand(HttpClient client, string url)
        {
            try
            {
                var response = await client.GetStringAsync(url);
                return JsonSerializer.Deserialize<SimCommand>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { return null; }
        }

        static void VypisDashboard(Car car, bool isPaused, double targetDist, int simMult)
        {
            Console.SetCursorPosition(0, 0);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("====================================================");
            Console.WriteLine("             DIGITÁLNÍ PALUBNÍ POČÍTAČ              ");
            Console.WriteLine("====================================================");
            Console.ResetColor();

            Console.Write(" STAV SYSTÉMU: ");
            if (isPaused) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("[||] POZASTAVENO UŽIVATELEM "); }
            else if (car.IsOverheating && car.CoolantTemp > 105) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[🔥] MOTOR SE PŘEHŘÍVÁ!      "); }
            else if (car.IsDefected) { Console.ForegroundColor = ConsoleColor.Magenta; Console.WriteLine($"[💥] DEFEKT! Oprava: {Math.Max(0, (int)car.RepairTimer)}s     "); }
            else if (car.IsPitStop) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("[!] SERVISNÍ ZASTÁVKA       "); }
            else { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("[OK] PROVOZ NORMÁLNÍ        "); }
            Console.ResetColor();

            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine($" Typ vozu:  {car.CarType,-10} | Nádrž: {car.FuelCapacity}L ");
            Console.WriteLine($" Rychlost:  {Math.Round(car.CurrentSpeed),3} km/h (Cíl: {car.TargetSpeed,3}) | Čas: {simMult}x");
            Console.WriteLine($" Ujeto:     {car.Distance,5:F2} / {targetDist:F2} km                      ");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine($" Palivo: {car.Fuel,5:F2} l | Olej: {car.OilLevel,5:F1}% | Voda: {car.CoolantTemp,5:F1}°C ");
            Console.WriteLine($" Pneu: {car.TirePressure,4:F2} bar | Spotřeba: {car.CurrentConsumptionPer100km,5:F1} l/100km");
        }

        static async Task OdesliData(HttpClient client, string url, Car car, double range, double targetDist, int simMult)
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
                    CoolantTemp = Math.Round(car.CoolantTemp, 1),
                    TirePressure = Math.Round(car.TirePressure, 2),
                    EstimatedRange = Math.Round(range, 1),
                    LightsOn = car.LightsOn,
                    WasherFluidLevel = Math.Round(car.WasherFluidLevel, 2),
                    IsDefected = car.IsDefected,
                    MaxFuelCapacity = car.FuelCapacity,
                    TargetDistance = targetDist,
                    SimSpeedMultiplier = simMult
                };
                var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
                await client.PostAsync(url, content);
            }
            catch { }
        }
    }

    public class SimCommand
    {
        public string Action { get; set; }
        public double TargetDistance { get; set; }
        public string CarType { get; set; }
    }

    class Car
    {
        public string CarType { get; set; } = "Sedan";
        public double FuelCapacity { get; set; } = 50.0;

        public double BaseConsumption { get; set; } = 3.5;
        public double AccelConsumption { get; set; } = 15.0;

        public double CurrentSpeed { get; set; } = 0;
        public double TargetSpeed { get; set; } = 0;
        public double Distance { get; set; } = 0;

        public double Fuel { get; set; } = 50.0;
        public double TotalFuelConsumed { get; private set; } = 0;
        public double CurrentConsumptionPer100km { get; private set; } = 0;

        public double OilLevel { get; set; } = 100.0;
        public double CoolantTemp { get; set; } = 85.0;
        public bool IsOverheating { get; set; } = false;

        public double TirePressure { get; set; } = 2.5;
        public double WasherFluidLevel { get; set; } = 100.0;

        public bool IsPitStop { get; set; } = false;
        public bool IsEmergencyExiting { get; set; } = false;
        public bool IsDefected { get; set; } = false;
        public double RepairTimer { get; set; } = 0;

        public bool LightsOn => CurrentSpeed > 0.5 || TargetSpeed > 0;

        public void ResetCar(string type)
        {
            CarType = type;

            if (type == "SUV")
            {
                FuelCapacity = 80;
                BaseConsumption = 6.0;
                AccelConsumption = 25.0;
            }
            else if (type == "Sport")
            {
                FuelCapacity = 40;
                BaseConsumption = 9.0;
                AccelConsumption = 45.0;
            }
            else // Sedan 
            {
                FuelCapacity = 50;
                BaseConsumption = 3.5;
                AccelConsumption = 15.0;
            }

            Fuel = FuelCapacity;
            Distance = 0;
            TotalFuelConsumed = 0;
            CurrentSpeed = 0;
            TargetSpeed = 50;
            OilLevel = 100;
            CoolantTemp = 85;
            TirePressure = 2.5;
            WasherFluidLevel = 100;
            IsPitStop = false;
            IsEmergencyExiting = false;
            IsDefected = false;
            IsOverheating = false;
        }

        public double GetAverageConsumption() => Distance > 0.1 ? (TotalFuelConsumed / Distance) * 100.0 : 0;

        public void Update(double dt, Random rnd)
        {
            if (IsPitStop && CurrentSpeed < 0.1)
            {
                bool isReady = true;
                if (Fuel < (FuelCapacity * 0.2)) { Fuel += 15 * dt; isReady = false; }
                if (OilLevel < 20) { OilLevel += 25 * dt; isReady = false; }
                if (TirePressure < 2.0) { TirePressure += 0.3 * dt; isReady = false; }
                if (CoolantTemp > 85.0) { CoolantTemp -= 8.0 * dt; isReady = false; }

                if (Fuel > FuelCapacity) Fuel = FuelCapacity;
                if (OilLevel > 100) OilLevel = 100;
                if (TirePressure > 2.5) TirePressure = 2.5;
                if (CoolantTemp < 85.0) CoolantTemp = 85.0;

                CurrentConsumptionPer100km = 0;
                if (isReady) { IsPitStop = false; TargetSpeed = 50; IsOverheating = false; }
            }
            else
            {
                double power = IsEmergencyExiting ? 5.0 : (IsDefected ? 25.0 : 20.0);

                if (CurrentSpeed < TargetSpeed) CurrentSpeed = Math.Min(TargetSpeed, CurrentSpeed + 12.0 * dt);
                else if (CurrentSpeed > TargetSpeed) CurrentSpeed = Math.Max(TargetSpeed, CurrentSpeed - power * dt);

                Distance += (CurrentSpeed / 3600.0) * dt;

                if (!IsOverheating)
                {
                    double targetNormal = 92.0 + (CurrentSpeed / 130.0) * 3.0;
                    CoolantTemp += (targetNormal - CoolantTemp) * 0.1 * dt + (rnd.NextDouble() - 0.5) * 0.4 * dt;
                }
                else
                {
                    CoolantTemp += (0.4 + (CurrentConsumptionPer100km / 50.0)) * dt;
                }

                if (CurrentSpeed < 1.0)
                {
                    Fuel -= (0.8 / 3600.0) * dt; // Volnoběh
                    TotalFuelConsumed += (0.8 / 3600.0) * dt;
                    CurrentConsumptionPer100km = 0;
                }
                else
                {
                    double speedFactor = CurrentSpeed / 100.0;
                    double steadyCons = BaseConsumption + (speedFactor * speedFactor * BaseConsumption);

                    if (CurrentSpeed < TargetSpeed) steadyCons += AccelConsumption;
                    else if (CurrentSpeed > TargetSpeed) steadyCons = 0.0; // Brzdění motorem

                    CurrentConsumptionPer100km = steadyCons;
                    double litersPerSec = (steadyCons / 100.0) * (CurrentSpeed / 3600.0);

                    Fuel -= litersPerSec * dt;
                    TotalFuelConsumed += litersPerSec * dt;
                }

                OilLevel -= 0.012 * dt;
                TirePressure -= 0.0004 * dt;
            }
        }
    }
}