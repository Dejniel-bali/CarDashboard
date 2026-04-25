using System;
using System.Threading;

namespace CarSimulator
{
    class Program
    {
        static void Main(string[] args)
        {
            Car car = new Car();
            Random rnd = new Random();
            int tickCount = 0;

            Console.CursorVisible = false;
            Console.Clear();
            Console.WriteLine("=== SIMULACE JÍZDY AUTA ===");
            Console.WriteLine("(Stiskněte CTRL+C pro ukončení)\n");

            // Hlavní smyčka programu běží, dokud nedojde palivo
            while (car.Fuel > 0)
            {
                // Každých 10 sekund se auto rozhodne, co bude dělat dál
                if (tickCount % 10 == 0)
                {
                    int randomEvent = rnd.Next(100);

                    if (randomEvent < 15)
                        car.TargetSpeed = 0;   // 15% šance na zastavení (křižovatka, překážka)
                    else if (randomEvent < 45)
                        car.TargetSpeed = 50;  // 30% šance na jízdu ve městě
                    else if (randomEvent < 75)
                        car.TargetSpeed = 90;  // 30% šance na okresku
                    else
                        car.TargetSpeed = 130; // 25% šance na dálnici
                }

                // Aktualizace fyziky auta (simulujeme posun o 1 sekundu)
                car.Update(1.0);

                // Vykreslení Dashboardu na stejné místo v konzoli (aby text neposkakoval)
                Console.SetCursorPosition(0, 3);
                Console.WriteLine($"Cílová rychlost:   {car.TargetSpeed,5} km/h   ");
                Console.WriteLine($"Aktuální rychlost: {Math.Round(car.CurrentSpeed),5} km/h   ");
                Console.WriteLine($"Ujetá vzdálenost:  {car.Distance,8:F2} km    ");
                Console.WriteLine($"Zbývající palivo:  {car.Fuel,8:F2} l     ");

                // Výpočet průměrné spotřeby (pokud už jsme něco ujeli)
                if (car.Distance > 0.1)
                {
                    double usedFuel = 50.0 - car.Fuel; // Výchozí nádrž je 50 litrů
                    double avgConsumption = (usedFuel / car.Distance) * 100.0;
                    Console.WriteLine($"Průměrná spotřeba: {avgConsumption,8:F2} l/100km   ");
                }
                else
                {
                    Console.WriteLine($"Průměrná spotřeba:   Počítám...         ");
                }

                // Počkáme 1 reálnou vteřinu (1000 ms)
                Thread.Sleep(1000);
                tickCount++;
            }

            Console.WriteLine("\nDOŠLO PALIVO! Auto zastavilo. Konec simulace.");
        }
    }

    class Car
    {
        public double CurrentSpeed { get; private set; } = 0;
        public double TargetSpeed { get; set; } = 0;
        public double Distance { get; private set; } = 0;
        public double Fuel { get; private set; } = 50.0; // Kapacita nádrže v litrech

        public void Update(double deltaTimeSeconds)
        {
            // --- 1. REALISTICKÉ ZRYCHLOVÁNÍ A BRZDĚNÍ ---
            double accelerationRate = 12.0; // Auto zrychlí o 12 km/h za sekundu
            double brakingRate = 20.0;      // Brzdy jsou silnější než motor (20 km/h za sekundu)

            if (CurrentSpeed < TargetSpeed)
            {
                CurrentSpeed += accelerationRate * deltaTimeSeconds;
                if (CurrentSpeed > TargetSpeed) CurrentSpeed = TargetSpeed; // Nepřekračovat cíl
            }
            else if (CurrentSpeed > TargetSpeed)
            {
                CurrentSpeed -= brakingRate * deltaTimeSeconds;
                if (CurrentSpeed < TargetSpeed) CurrentSpeed = TargetSpeed;
            }

            // --- 2. VÝPOČET VZDÁLENOSTI ---
            // Rychlost je v km/h, musíme ji převést na ujeté km za daný časový úsek (sekundy)
            double distanceThisTick = (CurrentSpeed / 3600.0) * deltaTimeSeconds;
            Distance += distanceThisTick;

            // --- 3. VÝPOČET SPOTŘEBY PALIVA ---
            // A) Spotřeba na volnoběh (např. když auto stojí s nastartovaným motorem)
            double idleConsumption = (0.8 / 3600.0) * deltaTimeSeconds; // cca 0.8 litru za hodinu

            // B) Spotřeba vlivem aerodynamického odporu (roste s druhou mocninou rychlosti)
            // Koeficient 0.0000002 je nastaven tak, aby při 130 km/h byla reálná spotřeba vyšší
            double speedConsumption = (CurrentSpeed * CurrentSpeed * 0.0000002) * deltaTimeSeconds;

            // C) Extra spotřeba paliva při akceleraci (motor se víc namáhá)
            double accelerationConsumption = 0;
            if (CurrentSpeed < TargetSpeed)
            {
                accelerationConsumption = (0.001) * deltaTimeSeconds;
            }

            // Odečteme celkovou spotřebu za tuto vteřinu z nádrže
            Fuel -= (idleConsumption + speedConsumption + accelerationConsumption);

            if (Fuel < 0) Fuel = 0;
        }
    }
}