using System;
using System.Collections.Generic;
using System.Linq;

namespace KnapsackGeneticAlgorithm
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            const int Capacity = 250;
            const int ItemCount = 100;
            const int PopulationSize = 100;
            const int MaxGenerations = 1000;
            const double MutationRate = 0.05;
            const int LocalSearchStartGen = 50; // Початок роботи Емуляції Відпалу (n-а ітерація)

            Random rand = new Random(42); // Фіксований Seed для стабільності та відтворюваності результатів

            // 1. Генерація предметів
            List<Item> items = new List<Item>();
            for (int i = 0; i < ItemCount; i++)
            {
                items.Add(new Item
                {
                    Id = i,
                    Value = rand.Next(2, 31),  // Цінність [2, 30]
                    Weight = rand.Next(1, 26)  // Вага [1, 25]
                });
            }

            // 2. Ініціалізація початкової популяції (100 осіб, кожна по 1 різному предмету)
            List<Individual> population = new List<Individual>();
            for (int i = 0; i < PopulationSize; i++)
            {
                Individual ind = new Individual(ItemCount);
                ind.Genes[i] = 1; // Кожен індивід має 1 унікальний предмет
                Evaluate(ind, items, Capacity);
                population.Add(ind);
            }

            Individual bestOverall = GetBest(population).Clone();

            Console.WriteLine("======================================================================");
            Console.WriteLine("  ЛАБОРАТОРНА РОБОТА №3. ЧАСТИНА 1 (Варіант 13)");
            Console.WriteLine("  Задача про рюкзак (P=250, N=100) + ГА + Емуляція відпалу");
            Console.WriteLine("======================================================================");
            Console.WriteLine($"{"Ітерація",-12} | {"Краща Цінність (ЦФ)",-22} | {"Вага рюкзака",-15} | {"Примітка",-15}");
            Console.WriteLine(new string('-', 72));

            // Зафіксуємо початковий стан (Ітерація 0)
            Console.WriteLine($"{0,-12} | {bestOverall.TotalValue,-22} | {bestOverall.TotalWeight + " / " + Capacity,-15} | {"Початкова",-15}");

            // Основний цикл Генетичного Алгоритму
            for (int gen = 1; gen <= MaxGenerations; gen++)
            {
                List<Individual> newPopulation = new List<Individual>();

                // Елітизм: переносимо кращого індивіда без змін
                Individual currentBest = GetBest(population).Clone();
                newPopulation.Add(currentBest);

                while (newPopulation.Count < PopulationSize)
                {
                    // Селекція (турнірна)
                    Individual parent1 = TournamentSelect(population, rand);
                    Individual parent2 = TournamentSelect(population, rand);

                    // Одноточкове схрещування 30% та 70% (точка розрізу = 30)
                    Individual child = Crossover30_70(parent1, parent2);

                    // Мутація (ймовірність 5%: обмін місцями 2 випадкових генів)
                    if (rand.NextDouble() < MutationRate)
                    {
                        MutateSwapTwoGenes(child, rand);
                    }

                    // Оператор локального покращення: Пошук із емуляцією відпалу (SA) з n-ї ітерації
                    if (gen >= LocalSearchStartGen)
                    {
                        SimulatedAnnealingLocalSearch(child, items, Capacity, rand);
                    }
                    else
                    {
                        RepairOrEvaluate(child, items, Capacity);
                    }

                    newPopulation.Add(child);
                }

                population = newPopulation;
                Individual bestInGen = GetBest(population);

                if (bestInGen.TotalValue > bestOverall.TotalValue)
                {
                    bestOverall = bestInGen.Clone();
                }

                // Вивід результатів кожні 20 ітерацій
                if (gen % 20 == 0)
                {
                    string note = gen >= LocalSearchStartGen ? "ГА + SA" : "Тільки ГА";
                    Console.WriteLine($"{gen,-12} | {bestOverall.TotalValue,-22} | {bestOverall.TotalWeight + " / " + Capacity,-15} | {note,-15}");
                }
            }

            Console.WriteLine(new string('-', 72));
            Console.WriteLine($"\nОстаточний кращий розв'язок:");
            Console.WriteLine($"-> Сумарна цінність (ЦФ): {bestOverall.TotalValue}");
            Console.WriteLine($"-> Сумарна вага:          {bestOverall.TotalWeight} / {Capacity}");
            Console.WriteLine($"-> Кількість предметів:   {bestOverall.Genes.Sum()} з {ItemCount}");
        }

        // Одноточковий кросовер: 30% від parent1, 70% від parent2
        static Individual Crossover30_70(Individual p1, Individual p2)
        {
            Individual child = new Individual(p1.Genes.Length);
            int splitIndex = (int)(p1.Genes.Length * 0.30); // 30 генів

            for (int i = 0; i < splitIndex; i++)
                child.Genes[i] = p1.Genes[i];

            for (int i = splitIndex; i < p1.Genes.Length; i++)
                child.Genes[i] = p2.Genes[i];

            return child;
        }

        // Мутація: обмін місцями 2 випадкових генів
        static void MutateSwapTwoGenes(Individual ind, Random rand)
        {
            int idx1 = rand.Next(ind.Genes.Length);
            int idx2 = rand.Next(ind.Genes.Length);
            while (idx1 == idx2)
            {
                idx2 = rand.Next(ind.Genes.Length);
            }

            int temp = ind.Genes[idx1];
            ind.Genes[idx1] = ind.Genes[idx2];
            ind.Genes[idx2] = temp;
        }

        // Локальний пошук: Емуляція відпалу (Simulated Annealing)
        static void SimulatedAnnealingLocalSearch(Individual ind, List<Item> items, int capacity, Random rand)
        {
            double temp = 100.0;
            double coolingRate = 0.92;
            double minTemp = 1.0;

            Individual current = ind.Clone();
            RepairOrEvaluate(current, items, capacity);

            while (temp > minTemp)
            {
                Individual neighbor = current.Clone();
                // Невеликий окіл: інвертуємо 1 випадковий ген
                int flipIdx = rand.Next(neighbor.Genes.Length);
                neighbor.Genes[flipIdx] = 1 - neighbor.Genes[flipIdx];
                RepairOrEvaluate(neighbor, items, capacity);

                int delta = neighbor.TotalValue - current.TotalValue;

                // Якщо сусід кращий або проходить за ймовірністю Больцмана
                if (delta > 0 || rand.NextDouble() < Math.Exp(delta / temp))
                {
                    current = neighbor;
                }

                temp *= coolingRate;
            }

            Array.Copy(current.Genes, ind.Genes, ind.Genes.Length);
            ind.TotalValue = current.TotalValue;
            ind.TotalWeight = current.TotalWeight;
        }

        // Оцінка та корекція рюкзака (якщо перевищено місткість, вилучаємо предмети з найгіршим співвідношенням цінність/вага)
        static void RepairOrEvaluate(Individual ind, List<Item> items, int capacity)
        {
            int weight = 0;
            int value = 0;

            for (int i = 0; i < ind.Genes.Length; i++)
            {
                if (ind.Genes[i] == 1)
                {
                    weight += items[i].Weight;
                    value += items[i].Value;
                }
            }

            // Штраф/Корекція штрафом перевищення ваги
            while (weight > capacity)
            {
                // Знаходимо включений предмет з найменшою питомою цінністю (Value/Weight)
                int bestToRemove = -1;
                double minRatio = double.MaxValue;

                for (int i = 0; i < ind.Genes.Length; i++)
                {
                    if (ind.Genes[i] == 1)
                    {
                        double ratio = (double)items[i].Value / items[i].Weight;
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            bestToRemove = i;
                        }
                    }
                }

                if (bestToRemove != -1)
                {
                    ind.Genes[bestToRemove] = 0;
                    weight -= items[bestToRemove].Weight;
                    value -= items[bestToRemove].Value;
                }
                else
                {
                    break;
                }
            }

            ind.TotalWeight = weight;
            ind.TotalValue = value;
        }

        static void Evaluate(Individual ind, List<Item> items, int capacity)
        {
            RepairOrEvaluate(ind, items, capacity);
        }

        static Individual TournamentSelect(List<Individual> pop, Random rand, int tournamentSize = 3)
        {
            Individual best = null;
            for (int i = 0; i < tournamentSize; i++)
            {
                Individual candidate = pop[rand.Next(pop.Count)];
                if (best == null || candidate.TotalValue > best.TotalValue)
                {
                    best = candidate;
                }
            }
            return best;
        }

        static Individual GetBest(List<Individual> pop)
        {
            return pop.OrderByDescending(x => x.TotalValue).First();
        }
    }
}