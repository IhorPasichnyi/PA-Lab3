using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BeeColonyTSP
{
    class Program
    {
        static int NumCities = 300;
        static int[,] DistanceMatrix;
        static Random rand = new Random(42); // Фіксований Seed для точності порівнянь

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=========================================================");
            Console.WriteLine("  ЧАСТИНА 2. Задача комівояжера (300 вершин, Змішана)");
            Console.WriteLine("  Бджолиний алгоритм (Bee Colony Optimization)");
            Console.WriteLine("=========================================================\n");

            // 1. Генерація змішаної матриці відстаней
            GenerateMixedDistanceMatrix(NumCities, 5, 150);

            // 2. Базовий запуск з оптимальними параметрами
            Console.WriteLine("--- 1. Базовий запуск алгоритму (Підібрані параметри) ---");
            BeeParams bestParams = new BeeParams
            {
                TotalBees = 50,
                ScoutBees = 10,
                BestSites = 5,
                EliteSites = 2,
                EliteBees = 15,
                OtherBees = 5,
                MaxIterations = 500
            };

            RunBeeAlgorithm(bestParams, verbose: true);

            // 3. Серія досліджень вхідних параметрів
            Console.WriteLine("\n=========================================================");
            Console.WriteLine("  ДОСЛІДЖЕННЯ ВХІДНИХ ПАРАМЕТРІВ АЛГОРИТМУ");
            Console.WriteLine("=========================================================");

            ResearchParameter_TotalBees();
            ResearchParameter_EliteBees();
            ResearchParameter_Sites();

            Console.WriteLine("\nДослідження завершено. Всі дані зібрані!");
        }

        static void GenerateMixedDistanceMatrix(int n, int minDist, int maxDist)
        {
            DistanceMatrix = new int[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        DistanceMatrix[i, j] = 0;
                    }
                    else
                    {
                        // 50% симетричних, 50% асиметричних ребер
                        if (j < i && rand.NextDouble() < 0.5)
                        {
                            DistanceMatrix[i, j] = DistanceMatrix[j, i];
                        }
                        else
                        {
                            DistanceMatrix[i, j] = rand.Next(minDist, maxDist + 1);
                        }
                    }
                }
            }
        }

        public class BeeParams
        {
            public int TotalBees;
            public int ScoutBees;
            public int BestSites;
            public int EliteSites;
            public int EliteBees;
            public int OtherBees;
            public int MaxIterations;
        }

        public class RouteSolution
        {
            public int[] Path;
            public int Distance;

            public RouteSolution(int n)
            {
                Path = new int[n];
            }

            public RouteSolution Clone()
            {
                RouteSolution copy = new RouteSolution(Path.Length);
                Array.Copy(this.Path, copy.Path, Path.Length);
                copy.Distance = this.Distance;
                return copy;
            }
        }

        public static int CalculateRouteDistance(int[] path)
        {
            int dist = 0;
            for (int i = 0; i < path.Length - 1; i++)
            {
                dist += DistanceMatrix[path[i], path[i + 1]];
            }
            dist += DistanceMatrix[path[path.Length - 1], path[0]];
            return dist;
        }

        public static RouteSolution CreateRandomRoute()
        {
            RouteSolution sol = new RouteSolution(NumCities);
            for (int i = 0; i < NumCities; i++) sol.Path[i] = i;

            // Fisher-Yates shuffle
            for (int i = NumCities - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                int temp = sol.Path[i];
                sol.Path[i] = sol.Path[j];
                sol.Path[j] = temp;
            }

            sol.Distance = CalculateRouteDistance(sol.Path);
            return sol;
        }

        public static RouteSolution Mutate2Opt(RouteSolution parent)
        {
            RouteSolution child = parent.Clone();
            int i = rand.Next(0, NumCities - 1);
            int j = rand.Next(i + 1, NumCities);

            // Реверс відрізка [i..j]
            Array.Reverse(child.Path, i, j - i + 1);
            child.Distance = CalculateRouteDistance(child.Path);
            return child;
        }

        public static RouteSolution RunBeeAlgorithm(BeeParams p, bool verbose = false)
        {
            List<RouteSolution> scouts = new List<RouteSolution>();
            for (int i = 0; i < p.ScoutBees; i++)
            {
                scouts.Add(CreateRandomRoute());
            }

            RouteSolution bestGlobal = scouts.OrderBy(s => s.Distance).First().Clone();

            if (verbose)
            {
                Console.WriteLine($"{"Ітерація",-10} | {"Краща довжина (ЦФ)",-22} | {"Стан рою",-15}");
                Console.WriteLine(new string('-', 55));
            }

            for (int iter = 1; iter <= p.MaxIterations; iter++)
            {
                scouts = scouts.OrderBy(s => s.Distance).ToList();

                if (scouts[0].Distance < bestGlobal.Distance)
                {
                    bestGlobal = scouts[0].Clone();
                }

                List<RouteSolution> nextGenScouts = new List<RouteSolution>();

                // 1. Пошук на елітних ділянках
                for (int i = 0; i < p.EliteSites && i < scouts.Count; i++)
                {
                    RouteSolution bestInSite = scouts[i].Clone();
                    for (int b = 0; b < p.EliteBees; b++)
                    {
                        RouteSolution neighbor = Mutate2Opt(scouts[i]);
                        if (neighbor.Distance < bestInSite.Distance)
                        {
                            bestInSite = neighbor;
                        }
                    }
                    nextGenScouts.Add(bestInSite);
                }

                // 2. Пошук на інших кращих ділянках
                for (int i = p.EliteSites; i < p.BestSites && i < scouts.Count; i++)
                {
                    RouteSolution bestInSite = scouts[i].Clone();
                    for (int b = 0; b < p.OtherBees; b++)
                    {
                        RouteSolution neighbor = Mutate2Opt(scouts[i]);
                        if (neighbor.Distance < bestInSite.Distance)
                        {
                            bestInSite = neighbor;
                        }
                    }
                    nextGenScouts.Add(bestInSite);
                }

                // 3. Випадкові розвідники для заповнення рою
                while (nextGenScouts.Count < p.ScoutBees)
                {
                    nextGenScouts.Add(CreateRandomRoute());
                }

                scouts = nextGenScouts;

                if (verbose && (iter % 50 == 0 || iter == 1))
                {
                    Console.WriteLine($"{iter,-10} | {bestGlobal.Distance,-22} | BCO Active");
                }
            }

            if (verbose)
            {
                Console.WriteLine(new string('-', 55));
                Console.WriteLine($"Підсумкова довжина маршруту: {bestGlobal.Distance} ум. од.\n");
            }

            return bestGlobal;
        }

        // --- Метод дослідження 1: Зміна загальної кількості бджіл ---
        static void ResearchParameter_TotalBees()
        {
            Console.WriteLine("\n[Дослідження 1]: Вплив кількості бджіл-розвідників (Scout Bees)");
            Console.WriteLine($"{"Кількість розвідників",-22} | {"Довжина маршруту (ЦФ)",-24} | {"Час (мс)",-10}");
            Console.WriteLine(new string('-', 60));

            int[] testBees = new int[] { 5, 10, 20, 35, 50 };

            foreach (int scoutsCount in testBees)
            {
                BeeParams p = new BeeParams
                {
                    ScoutBees = scoutsCount,
                    BestSites = Math.Max(2, scoutsCount / 2),
                    EliteSites = Math.Max(1, scoutsCount / 4),
                    EliteBees = 15,
                    OtherBees = 5,
                    MaxIterations = 300
                };

                Stopwatch sw = Stopwatch.StartNew();
                RouteSolution res = RunBeeAlgorithm(p, verbose: false);
                sw.Stop();

                Console.WriteLine($"{scoutsCount,-22} | {res.Distance,-24} | {sw.ElapsedMilliseconds,-10}");
            }
        }

        // --- Метод дослідження 2: Зміна кількості бджіл на елітних ділянках ---
        static void ResearchParameter_EliteBees()
        {
            Console.WriteLine("\n[Дослідження 2]: Вплив кількості бджіл на елітній ділянці (Elite Bees)");
            Console.WriteLine($"{"Фуражирів на еліту",-22} | {"Довжина маршруту (ЦФ)",-24} | {"Час (мс)",-10}");
            Console.WriteLine(new string('-', 60));

            int[] testEliteBees = new int[] { 2, 5, 10, 20, 40 };

            foreach (int eliteBees in testEliteBees)
            {
                BeeParams p = new BeeParams
                {
                    ScoutBees = 15,
                    BestSites = 6,
                    EliteSites = 2,
                    EliteBees = eliteBees,
                    OtherBees = 4,
                    MaxIterations = 300
                };

                Stopwatch sw = Stopwatch.StartNew();
                RouteSolution res = RunBeeAlgorithm(p, verbose: false);
                sw.Stop();

                Console.WriteLine($"{eliteBees,-22} | {res.Distance,-24} | {sw.ElapsedMilliseconds,-10}");
            }
        }

        // --- Метод дослідження 3: Зміна кількості обраних ділянок ---
        static void ResearchParameter_Sites()
        {
            Console.WriteLine("\n[Дослідження 3]: Вплив кількості кращих/елітних ділянок (Best Sites)");
            Console.WriteLine($"{"Обраних ділянок (m)",-22} | {"Довжина маршруту (ЦФ)",-24} | {"Час (мс)",-10}");
            Console.WriteLine(new string('-', 60));

            int[] testSites = new int[] { 2, 4, 6, 10, 15 };

            foreach (int sites in testSites)
            {
                BeeParams p = new BeeParams
                {
                    ScoutBees = 20,
                    BestSites = sites,
                    EliteSites = Math.Max(1, sites / 2),
                    EliteBees = 12,
                    OtherBees = 4,
                    MaxIterations = 300
                };

                Stopwatch sw = Stopwatch.StartNew();
                RouteSolution res = RunBeeAlgorithm(p, verbose: false);
                sw.Stop();

                Console.WriteLine($"{sites,-22} | {res.Distance,-24} | {sw.ElapsedMilliseconds,-10}");
            }
        }
    }
}