// Лабораторна робота №3, частина 1, варіант 13
// Задача про рюкзак: P = 250, 100 предметів, цінність 2..30, вага 1..25.
// Генетичний алгоритм:
//   - початкова популяція: 100 особин, кожна містить рівно 1 (різний) предмет;
//   - схрещування: одноточкове, 30% генів від одного батька і 70% від іншого;
//   - мутація: з ймовірністю 5% два випадкові гени міняються місцями;
//   - локальне покращення: пошук з емуляцією відпалу (з n-ї ітерації).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KnapsackGA
{
    // ----------------------------------------------------------------
    //  Особина: бітовий рядок (genes[i] == true -> предмет i у рюкзаку)
    // ----------------------------------------------------------------
    class Individual
    {
        public bool[] Genes;
        public int Value;
        public int Weight;

        public Individual(int n)
        {
            Genes = new bool[n];
        }

        public Individual Clone()
        {
            var c = new Individual(Genes.Length);
            Array.Copy(Genes, c.Genes, Genes.Length);
            c.Value = Value;
            c.Weight = Weight;
            return c;
        }

        public void Evaluate(int[] values, int[] weights)
        {
            int v = 0, w = 0;
            for (int i = 0; i < Genes.Length; i++)
            {
                if (Genes[i])
                {
                    v += values[i];
                    w += weights[i];
                }
            }
            Value = v;
            Weight = w;
        }
    }

    class Program
    {
        // ------------------- Параметри задачі -------------------
        const int Capacity = 250;
        const int ItemCount = 100;
        const int MinValue = 2, MaxValue = 30;     // цінність (включно)
        const int MinWeight = 1, MaxWeight = 25;   // вага (включно)

        // ------------------- Параметри алгоритму -------------------
        const int PopulationSize = 100;
        const double CrossoverSplit = 0.30;        // точка розрізу: 30% / 70%
        const double MutationProbability = 0.05;   // ймовірність мутації
        const int TournamentSize = 3;              // розмір турніру при відборі батьків
        const int MaxIterations = 1000;            // верхня межа ітерацій
        const int PrintStep = 20;                  // крок виводу в таблицю (1 = кожна ітерація)

        // Локальне покращення (відпал) вмикається з ітерації LocalSearchStart
        const int LocalSearchStart = 200;
        const double SaStartTemp = 10.0;
        const double SaEndTemp = 0.05;
        const int SaSteps = 300;

        // 0 -> випадкове зерно (різні результати щоразу), інше число -> відтворюваний результат
        const int Seed = 13;

        static Random rng;
        static int[] values = new int[ItemCount];
        static int[] weights = new int[ItemCount];

        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            rng = Seed == 0 ? new Random() : new Random(Seed);

            GenerateItems();
            PrintInstance();

            // ---------- Початкова популяція: особина i містить лише предмет i ----------
            var population = new List<Individual>(PopulationSize);
            for (int i = 0; i < PopulationSize; i++)
            {
                var ind = new Individual(ItemCount);
                ind.Genes[i % ItemCount] = true;
                ind.Evaluate(values, weights);
                population.Add(ind);
            }

            Individual best = BestOf(population).Clone();

            Console.WriteLine();
            Console.WriteLine("Параметри: P={0}, популяція={1}, схрещування {2:0}%/{3:0}%, мутація={4:0}%, " +
                              "відпал з ітерації {5}", Capacity, PopulationSize,
                              CrossoverSplit * 100, (1 - CrossoverSplit) * 100,
                              MutationProbability * 100, LocalSearchStart);
            Console.WriteLine();

            string line = new string('-', 52);
            Console.WriteLine(line);
            Console.WriteLine("| {0,10} | {1,16} | {2,16} |", "Ітерація", "Кращий розв'язок", "Вага рюкзака");
            Console.WriteLine(line);
            Console.WriteLine("| {0,10} | {1,16} | {2,16} |", 0, best.Value, best.Weight);

            // ---------- Основний цикл ----------
            for (int iter = 1; iter <= MaxIterations; iter++)
            {
                population = NextGeneration(population, best);

                // Локальне покращення: відпал для кращої особини поточної популяції
                if (iter >= LocalSearchStart)
                {
                    int bi = IndexOfBest(population);
                    Individual improved = Anneal(population[bi]);
                    if (improved.Value > population[bi].Value)
                        population[bi] = improved;
                }

                Individual currentBest = BestOf(population);
                if (currentBest.Value > best.Value)
                    best = currentBest.Clone();

                if (iter % PrintStep == 0 || iter == MaxIterations)
                    Console.WriteLine("| {0,10} | {1,16} | {2,16} |", iter, best.Value, best.Weight);
            }
            Console.WriteLine(line);

            PrintSolution(best);
        }

        // ================= Генерація даних =================
        static void GenerateItems()
        {
            for (int i = 0; i < ItemCount; i++)
            {
                values[i] = rng.Next(MinValue, MaxValue + 1);
                weights[i] = rng.Next(MinWeight, MaxWeight + 1);
            }
        }

        static void PrintInstance()
        {
            Console.WriteLine("Предмети (№: цінність/вага):");
            for (int i = 0; i < ItemCount; i++)
            {
                Console.Write("{0,3}: {1,2}/{2,2}   ", i + 1, values[i], weights[i]);
                if ((i + 1) % 8 == 0) Console.WriteLine();
            }
            Console.WriteLine();
        }

        // ================= Генетичний алгоритм =================
        static List<Individual> NextGeneration(List<Individual> population, Individual globalBest)
        {
            var next = new List<Individual>(PopulationSize);

            // Елітизм: найкращий відомий розв'язок переходить у наступне покоління
            next.Add(globalBest.Clone());

            while (next.Count < PopulationSize)
            {
                Individual p1 = Tournament(population);
                Individual p2 = Tournament(population);

                Individual c1, c2;
                Crossover(p1, p2, out c1, out c2);

                Mutate(c1);
                Mutate(c2);

                Repair(c1);
                Repair(c2);

                next.Add(c1);
                if (next.Count < PopulationSize)
                    next.Add(c2);
            }
            return next;
        }

        // Турнірний відбір
        static Individual Tournament(List<Individual> population)
        {
            Individual winner = population[rng.Next(population.Count)];
            for (int i = 1; i < TournamentSize; i++)
            {
                Individual challenger = population[rng.Next(population.Count)];
                if (challenger.Value > winner.Value)
                    winner = challenger;
            }
            return winner;
        }

        // Одноточкове схрещування: перші 30% генів від одного батька, решта 70% від іншого
        static void Crossover(Individual a, Individual b, out Individual c1, out Individual c2)
        {
            int cut = (int)Math.Round(ItemCount * CrossoverSplit); // = 30
            c1 = new Individual(ItemCount);
            c2 = new Individual(ItemCount);
            for (int i = 0; i < ItemCount; i++)
            {
                if (i < cut)
                {
                    c1.Genes[i] = a.Genes[i];
                    c2.Genes[i] = b.Genes[i];
                }
                else
                {
                    c1.Genes[i] = b.Genes[i];
                    c2.Genes[i] = a.Genes[i];
                }
            }
        }

        // Мутація: з ймовірністю 5% два випадкові гени міняються місцями
        static void Mutate(Individual ind)
        {
            if (rng.NextDouble() >= MutationProbability)
                return;

            int i = rng.Next(ItemCount);
            int j = rng.Next(ItemCount);
            while (j == i)
                j = rng.Next(ItemCount);

            bool tmp = ind.Genes[i];
            ind.Genes[i] = ind.Genes[j];
            ind.Genes[j] = tmp;
        }

        // Виправлення недопустимих розв'язків: поки вага перевищує місткість,
        // викидаємо предмет з найгіршим відношенням цінність/вага.
        // Після виконання оцінка (Value, Weight) особини актуальна.
        static void Repair(Individual ind)
        {
            ind.Evaluate(values, weights);
            while (ind.Weight > Capacity)
            {
                int worst = -1;
                double worstRatio = double.MaxValue;
                for (int i = 0; i < ItemCount; i++)
                {
                    if (!ind.Genes[i]) continue;
                    double r = (double)values[i] / weights[i];
                    if (r < worstRatio)
                    {
                        worstRatio = r;
                        worst = i;
                    }
                }
                ind.Genes[worst] = false;
                ind.Value -= values[worst];
                ind.Weight -= weights[worst];
            }
        }

        // ================= Локальне покращення: емуляція відпалу =================
        static Individual Anneal(Individual start)
        {
            Individual current = start.Clone();
            Individual bestLocal = current.Clone();

            double alpha = Math.Pow(SaEndTemp / SaStartTemp, 1.0 / SaSteps);
            double temp = SaStartTemp;

            for (int step = 0; step < SaSteps; step++)
            {
                Individual neighbour = RandomNeighbour(current);
                int delta = neighbour.Value - current.Value;

                if (delta >= 0 || rng.NextDouble() < Math.Exp(delta / temp))
                {
                    current = neighbour;
                    if (current.Value > bestLocal.Value)
                        bestLocal = current.Clone();
                }
                temp *= alpha;
            }
            return bestLocal;
        }

        // Сусід: інвертуємо випадковий ген. Якщо предмет додано і рюкзак переповнений,
        // випадково викидаємо інші предмети, доки розв'язок не стане допустимим.
        static Individual RandomNeighbour(Individual src)
        {
            Individual n = src.Clone();
            int k = rng.Next(ItemCount);

            if (n.Genes[k])
            {
                n.Genes[k] = false;
                n.Value -= values[k];
                n.Weight -= weights[k];
                return n;
            }

            n.Genes[k] = true;
            n.Value += values[k];
            n.Weight += weights[k];

            while (n.Weight > Capacity)
            {
                int j = rng.Next(ItemCount);
                if (j == k || !n.Genes[j]) continue;
                n.Genes[j] = false;
                n.Value -= values[j];
                n.Weight -= weights[j];
            }
            return n;
        }

        // ================= Допоміжні функції =================
        static int IndexOfBest(List<Individual> pop)
        {
            int bi = 0;
            for (int i = 1; i < pop.Count; i++)
                if (pop[i].Value > pop[bi].Value)
                    bi = i;
            return bi;
        }

        static Individual BestOf(List<Individual> pop)
        {
            return pop[IndexOfBest(pop)];
        }

        // Точний оптимум динамічним програмуванням (для порівняння)
        static int ExactOptimum()
        {
            int[] dp = new int[Capacity + 1];
            for (int i = 0; i < ItemCount; i++)
                for (int w = Capacity; w >= weights[i]; w--)
                    dp[w] = Math.Max(dp[w], dp[w - weights[i]] + values[i]);
            return dp[Capacity];
        }

        static void PrintSolution(Individual best)
        {
            Console.WriteLine();
            Console.WriteLine("Найкращий знайдений розв'язок:");
            Console.WriteLine("  Цінність: {0}", best.Value);
            Console.WriteLine("  Вага:     {0} / {1}", best.Weight, Capacity);
            Console.Write("  Предмети (№): ");
            Console.WriteLine(string.Join(", ",
                Enumerable.Range(0, ItemCount).Where(i => best.Genes[i]).Select(i => (i + 1).ToString())));

            int opt = ExactOptimum();
            Console.WriteLine();
            Console.WriteLine("Точний оптимум (динамічне програмування): {0}", opt);
            Console.WriteLine("Відхилення від оптимуму: {0} ({1:0.00}%)",
                opt - best.Value, 100.0 * (opt - best.Value) / opt);
        }
    }
}