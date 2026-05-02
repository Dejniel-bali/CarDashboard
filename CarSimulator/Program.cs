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
            using HttpClient client = new HttpClient();
            string serverUrl = "https://localhost:7285/api/telemetry";
            string commandUrl = "https://localhost:7285/api/command";

            Console.CursorVisible = false;
            bool isRunning = false;
            double targetDistance = 50.0;
            int simMultiplier = 1;
            double exitTimer = 0;
            int tickCount = 0; // VRÁCENO: Počítadlo času pro změnu rychlosti

            while (true)
            {
                var cmd = await FetchCommand(client, commandUrl);
                if (cmd != null && !string.IsNullOrEmpty(cmd.Action))
                {
                    switch (cmd.Action)
                    {
                        case "start":
                            car.ResetCar(cmd.CarType);
                            targetDistance = cmd.TargetDistance > 0 ? cmd.TargetDistance : 50.0;
                            isRunning = true;
                            simMultiplier = 1;
                            break;
                        case "stop": isRunning = false; break;
                        case "sim_faster":
                            if (simMultiplier == 1) simMultiplier = 2;
                            else if (simMultiplier == 2) simMultiplier = 3;
                            else if (simMultiplier == 3) simMultiplier = 5;
                            else if (simMultiplier == 5) simMultiplier = 10;
                            break;
                        case "sim_slower":
                            if (simMultiplier == 10) simMultiplier = 5;
                            else if (simMultiplier == 5) simMultiplier = 3;
                            else if (simMultiplier == 3) simMultiplier = 2;
                            else if (simMultiplier == 2) simMultiplier = 1;
                            break;
                        case "drop_oil": car.OilLevel = 4.0; break;
                        case "drop_water": car.IsOverheating = true; break;

                        // Defekty kol
                        case "defect_fl": car.TireFL = 0.5; car.IsDefected = true; car.RepairTimer = 10; car.TargetSpeed = 0; break;
                        case "defect_fr": car.TireFR = 0.5; car.IsDefected = true; car.RepairTimer = 10; car.TargetSpeed = 0; break;
                        case "defect_rl": car.TireRL = 0.5; car.IsDefected = true; car.RepairTimer = 10; car.TargetSpeed = 0; break;
                        case "defect_rr": car.TireRR = 0.5; car.IsDefected = true; car.RepairTimer = 10; car.TargetSpeed = 0; break;
                    }
                }

                if (isRunning && car.Distance < targetDistance)
                {
                    if (car.IsDefected && car.CurrentSpeed < 0.5)
                    {
                        car.RepairTimer -= simMultiplier;
                        if (car.RepairTimer <= 0) { car.IsDefected = false; car.TargetSpeed = 50; }
                    }

                    // VRÁCENO: Náhodné změny povolené rychlosti (mozek řidiče)
                    if (!car.IsPitStop && !car.IsEmergencyExiting && !car.IsDefected)
                    {
                        if (tickCount % 10 == 0) // Každých 10 cyklů je šance na změnu značky
                        {
                            int randomEvent = rnd.Next(100);
                            if (car.TargetSpeed == 0) car.TargetSpeed = 50;
                            else if (car.TargetSpeed == 130) car.TargetSpeed = randomEvent < 30 ? 90 : 130;
                            else car.TargetSpeed = rnd.Next(3) == 0 ? 50 : (rnd.Next(2) == 0 ? 90 : 130);
                        }
                    }

                    bool isCritical = car.OilLevel < 5.0 || car.CoolantTemp >= 120.0 || car.Fuel < 2.0 ||
                                      car.TireFL < 1.8 || car.TireFR < 1.8 || car.TireRL < 1.8 || car.TireRR < 1.8;

                    if (isCritical && !car.IsPitStop && !car.IsDefected)
                    {
                        if (car.CurrentSpeed > 95 && !car.IsEmergencyExiting)
                        { car.IsEmergencyExiting = true; car.TargetSpeed = 90; exitTimer = 5; }
                        else if (car.IsEmergencyExiting)
                        {
                            if (car.CurrentSpeed <= 91)
                            {
                                if (exitTimer > 0) exitTimer -= simMultiplier;
                                else { car.IsEmergencyExiting = false; car.IsPitStop = true; car.TargetSpeed = 0; }
                            }
                        }
                        else if (!car.IsEmergencyExiting) { car.IsPitStop = true; car.TargetSpeed = 0; }
                    }

                    car.Update(1.0 * simMultiplier, rnd);
                    await OdesliData(client, serverUrl, car, targetDistance, simMultiplier);
                }

                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"Jízda: {car.TripId} | Typ: {car.CarType} | Ujeto: {car.Distance:F2} km | Cíl: {car.TargetSpeed} km/h");
                await Task.Delay(1000);
                tickCount++; // VRÁCENO: Inkrementace času
            }
        }

        static async Task<SimCommand> FetchCommand(HttpClient client, string url)
        {
            try { var r = await client.GetStringAsync(url); return JsonSerializer.Deserialize<SimCommand>(r, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { return null; }
        }

        static async Task OdesliData(HttpClient client, string url, Car car, double targetDist, int simMult)
        {
            var data = new
            {
                TripId = car.TripId,
                CarType = car.CarType,
                Timestamp = DateTime.UtcNow,
                CurrentSpeed = car.CurrentSpeed,
                TargetSpeed = car.TargetSpeed,
                Distance = car.Distance,
                FuelRemaining = car.Fuel,
                CurrentConsumption = car.CurrentConsumption,
                AverageConsumption = car.GetAverageConsumption(),
                OilLevel = car.OilLevel,
                CoolantTemp = car.CoolantTemp,
                EstimatedRange = car.GetAverageConsumption() > 0 ? (car.Fuel / car.GetAverageConsumption() * 100) : 0,
                TireFL = car.TireFL,
                TireFR = car.TireFR,
                TireRL = car.TireRL,
                TireRR = car.TireRR,
                LightsOn = car.LightsOn,
                WasherFluidLevel = 100.0,
                IsDefected = car.IsDefected,
                MaxFuelCapacity = car.FuelCapacity,
                TargetDistance = targetDist,
                SimSpeedMultiplier = simMult
            };
            try { await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")); } catch { }
        }
    }

    class Car
    {
        public string TripId { get; set; } = "";
        public string CarType { get; set; } = "Sedan";
        public double Distance { get; set; } = 0;
        public double CurrentSpeed { get; set; } = 0;
        public double TargetSpeed { get; set; } = 50;
        public double Fuel { get; set; } = 50;
        public double FuelCapacity { get; set; } = 50;
        public double TotalFuelConsumed { get; set; } = 0;
        public double CurrentConsumption { get; set; } = 0;

        public double BaseConsumption { get; set; } = 3.5;
        public double AccelConsumption { get; set; } = 15.0;

        public double OilLevel { get; set; } = 100;
        public double CoolantTemp { get; set; } = 85;
        public bool IsOverheating { get; set; } = false;

        public double TireFL { get; set; } = 2.4;
        public double TireFR { get; set; } = 2.4;
        public double TireRL { get; set; } = 2.3;
        public double TireRR { get; set; } = 2.3;

        public bool IsPitStop { get; set; } = false;
        public bool IsEmergencyExiting { get; set; } = false;
        public bool IsDefected { get; set; } = false;
        public double RepairTimer { get; set; } = 0;

        public bool LightsOn => CurrentSpeed > 0.5 || TargetSpeed > 0;

        public void ResetCar(string type)
        {
            TripId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            CarType = type; Distance = 0; TotalFuelConsumed = 0; CurrentSpeed = 0; TargetSpeed = 50;
            OilLevel = 100; CoolantTemp = 85;
            TireFL = 2.4; TireFR = 2.4; TireRL = 2.3; TireRR = 2.3;
            IsPitStop = false; IsEmergencyExiting = false; IsDefected = false; IsOverheating = false;

            if (type == "SUV") { FuelCapacity = 80; BaseConsumption = 6.0; AccelConsumption = 25.0; }
            else if (type == "Sport") { FuelCapacity = 40; BaseConsumption = 9.0; AccelConsumption = 45.0; }
            else { FuelCapacity = 50; BaseConsumption = 3.5; AccelConsumption = 15.0; }
            Fuel = FuelCapacity;
        }

        public double GetAverageConsumption() => Distance > 0.1 ? (TotalFuelConsumed / Distance) * 100.0 : 0;

        public void Update(double dt, Random rnd)
        {
            if (IsPitStop && CurrentSpeed < 0.1)
            {
                bool isReady = true;
                if (Fuel < (FuelCapacity * 0.2)) { Fuel += 15 * dt; isReady = false; }
                if (OilLevel < 20) { OilLevel += 25 * dt; isReady = false; }
                if (CoolantTemp > 85.0) { CoolantTemp -= 8.0 * dt; isReady = false; }

                if (TireFL < 2.4) { TireFL += 0.5 * dt; isReady = false; }
                if (TireFR < 2.4) { TireFR += 0.5 * dt; isReady = false; }
                if (TireRL < 2.3) { TireRL += 0.5 * dt; isReady = false; }
                if (TireRR < 2.3) { TireRR += 0.5 * dt; isReady = false; }

                if (TireFL > 2.4) TireFL = 2.4;
                if (TireFR > 2.4) TireFR = 2.4;
                if (TireRL > 2.3) TireRL = 2.3;
                if (TireRR > 2.3) TireRR = 2.3;

                if (Fuel > FuelCapacity) Fuel = FuelCapacity;
                if (OilLevel > 100) OilLevel = 100;
                if (CoolantTemp < 85.0) CoolantTemp = 85.0;

                CurrentConsumption = 0;
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
                    CoolantTemp += (0.4 + (CurrentConsumption / 50.0)) * dt;
                }

                if (CurrentSpeed < 1.0)
                {
                    Fuel -= (0.8 / 3600.0) * dt;
                    TotalFuelConsumed += (0.8 / 3600.0) * dt;
                    CurrentConsumption = 0;
                }
                else
                {
                    double speedFactor = CurrentSpeed / 100.0;
                    double steadyCons = BaseConsumption + (speedFactor * speedFactor * BaseConsumption);
                    if (CurrentSpeed < TargetSpeed) steadyCons += AccelConsumption;
                    else if (CurrentSpeed > TargetSpeed) steadyCons = 0.0;

                    CurrentConsumption = steadyCons;
                    double litersPerSec = (steadyCons / 100.0) * (CurrentSpeed / 3600.0);
                    Fuel -= litersPerSec * dt;
                    TotalFuelConsumed += litersPerSec * dt;
                }

                OilLevel -= 0.012 * dt;
                TireFL -= 0.0001 * dt; TireFR -= 0.0001 * dt;
                TireRL -= 0.0001 * dt; TireRR -= 0.0001 * dt;
            }
        }
    }
    public class SimCommand { public string Action { get; set; } public double TargetDistance { get; set; } public string CarType { get; set; } }
}