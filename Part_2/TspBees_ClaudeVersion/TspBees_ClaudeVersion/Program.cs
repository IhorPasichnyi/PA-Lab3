// Задача комівояжера (змішана мережа, 300 вершин, відстані 5..150)
// Бджолиний алгоритм (Bees Algorithm / Bee Colony Optimization)
//
// Схема алгоритму (одна ітерація):
//   1. Усі розв'язки (ділянки) сортуються за довжиною маршруту.
//   2. Елітні ділянки (e найкращих) досліджуються nep фуражирами кожна,
//      решта вибраних ділянок (m - e) - nsp фуражирами кожна.
//      Фуражир робить один локальний крок (2-opt / переміщення міста / обмін)
//      у напрямку "перспективного" ребра зі списку найближчих кандидатів.
//      Якщо найкращий сусід кращий за ділянку - він замінює її.
//   3. Решта бджіл (n - m) - розвідники: будують нові випадково-жадібні маршрути.

using System;
using System.Linq;
using System.Text;

namespace TspBees
{
    class Tour
    {
        public int[] Order;
        public int Length;
    }

    class Program
    {
        // ===================== Параметри задачі =====================
        const int N = 300;
        const int MinDist = 5;
        const int MaxDist = 150;
        const double SymmetricProbability = 0.5; // частка пар вершин із симетричними ребрами
        const int Seed = 42;

        // ===================== Параметри рою =====================
        const int Scouts = 30;          // n  - загальна кількість розвідників (розмір популяції ділянок)
        const int SelectedSites = 10;   // m  - кількість обраних ділянок
        const int EliteSites = 3;       // e  - кількість елітних ділянок (e <= m)
        const int EliteForagers = 40;   // nep - фуражирів на кожній елітній ділянці
        const int OtherForagers = 12;   // nsp - фуражирів на кожній іншій обраній ділянці
        const int Iterations = 8000;    // кількість ітерацій
        const int ReportStep = 50;      // крок виводу прогресу

        // Допоміжні параметри
        const int CandidateCount = 10;  // довжина списку найближчих кандидатів для кожної вершини
        const int GreedyChoices = 3;    // у випадково-жадібному конструкторі вибір серед N найближчих непройдених

        static int[] dist = new int[N * N]; // dist[i*N + j] - вартість переходу i -> j
        static int[] cand = new int[N * CandidateCount];
        static Random rng;

        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            rng = new Random(Seed);

            GenerateMixedMatrix();
            BuildCandidateLists();
            PrintParameters();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // ---------- Початкова популяція ділянок ----------
            Tour[] pop = new Tour[Scouts];
            for (int i = 0; i < Scouts; i++)
                pop[i] = ConstructRandomGreedy();
            Array.Sort(pop, (x, y) => x.Length.CompareTo(y.Length));

            Tour best = CloneTour(pop[0]);

            Console.WriteLine("{0,10} | {1,20}", "Ітерація", "Краща довжина");
            Console.WriteLine(new string('-', 33));
            Console.WriteLine("{0,10} | {1,20}", 0, best.Length);

            int[] pos = new int[N];
            int[] tmp = new int[N];
            int[] bestNeighbour = new int[N];

            // ---------- Основний цикл ----------
            for (int iter = 1; iter <= Iterations; iter++)
            {
                // Пошук фуражирами на обраних ділянках
                for (int s = 0; s < SelectedSites; s++)
                {
                    int foragers = s < EliteSites ? EliteForagers : OtherForagers;
                    ImproveSite(pop[s], foragers, pos, tmp, bestNeighbour);
                }

                // Розвідники: нові випадкові розв'язки замість необраних ділянок
                for (int s = SelectedSites; s < Scouts; s++)
                    pop[s] = ConstructRandomGreedy();

                Array.Sort(pop, (x, y) => x.Length.CompareTo(y.Length));

                if (pop[0].Length < best.Length)
                    best = CloneTour(pop[0]);

                if (iter % ReportStep == 0)
                    Console.WriteLine("{0,10} | {1,20}", iter, best.Length);
            }

            sw.Stop();

            // ---------- Результат ----------
            int check = TourLength(best.Order);
            Console.WriteLine();
            Console.WriteLine("Остаточна довжина найкоротшого знайденого маршруту: {0} у.о.", check);
            Console.WriteLine("Час роботи: {0:0.0} с", sw.Elapsed.TotalSeconds);
            Console.WriteLine();
            Console.WriteLine("Маршрут (замкнений, нумерація вершин з 1):");
            Console.WriteLine(string.Join(" -> ", best.Order.Select(v => (v + 1).ToString())) +
                              " -> " + (best.Order[0] + 1));
        }

        // =============== Генерація змішаної матриці відстаней ===============
        // Для кожної пари i<j: d[i,j] випадкове. З ймовірністю 0.5 ребро симетричне
        // (d[j,i] = d[i,j]), інакше d[j,i] - окреме випадкове значення.
        static void GenerateMixedMatrix()
        {
            int symmetricPairs = 0;
            int totalPairs = N * (N - 1) / 2;

            for (int i = 0; i < N; i++)
            {
                for (int j = i + 1; j < N; j++)
                {
                    int d1 = rng.Next(MinDist, MaxDist + 1);
                    int d2;
                    if (rng.NextDouble() < SymmetricProbability)
                    {
                        d2 = d1;
                        symmetricPairs++;
                    }
                    else
                    {
                        d2 = rng.Next(MinDist, MaxDist + 1);
                    }
                    dist[i * N + j] = d1;
                    dist[j * N + i] = d2;
                }
            }

            Console.WriteLine("Згенеровано змішану мережу: {0} вершин, seed={1}", N, Seed);
            Console.WriteLine("Пар вершин із симетричною відстанню (за значенням): {0} з {1}",
                              Enumerable.Range(0, N).Sum(i => Enumerable.Range(i + 1, N - i - 1)
                                  .Count(j => dist[i * N + j] == dist[j * N + i])), totalPairs);
            Console.WriteLine("Пар, симетричних за побудовою: {0} ({1:0.0}%)",
                              symmetricPairs, 100.0 * symmetricPairs / totalPairs);
            Console.WriteLine();
        }

        // Для кожної вершини - K найближчих за вихідною відстанню
        static void BuildCandidateLists()
        {
            for (int a = 0; a < N; a++)
            {
                int[] others = Enumerable.Range(0, N).Where(x => x != a).ToArray();
                int[] keys = others.Select(x => dist[a * N + x]).ToArray();
                Array.Sort(keys, others);
                for (int k = 0; k < CandidateCount; k++)
                    cand[a * CandidateCount + k] = others[k];
            }
        }

        static void PrintParameters()
        {
            Console.WriteLine("Підсумкові параметри бджолиного алгоритму:");
            Console.WriteLine("  Бджоли-розвідники (n):                    {0}", Scouts);
            Console.WriteLine("  Обрані ділянки (m):                       {0}", SelectedSites);
            Console.WriteLine("  Елітні ділянки (e):                       {0}", EliteSites);
            Console.WriteLine("  Фуражири на елітній ділянці (nep):        {0}", EliteForagers);
            Console.WriteLine("  Фуражири на іншій обраній ділянці (nsp):  {0}", OtherForagers);
            Console.WriteLine("  Кількість ітерацій:                       {0}", Iterations);
            Console.WriteLine("  Довжина списку кандидатів:                {0}", CandidateCount);
            Console.WriteLine();
        }

        // =============== Конструктор розв'язку розвідника ===============
        // Випадкова стартова вершина; наступна обирається випадково серед кількох
        // найближчих ще не відвіданих (за списком кандидатів), інакше - найближча з усіх.
        static Tour ConstructRandomGreedy()
        {
            int[] order = new int[N];
            bool[] visited = new bool[N];
            int cur = rng.Next(N);
            order[0] = cur;
            visited[cur] = true;

            int[] pick = new int[GreedyChoices];

            for (int step = 1; step < N; step++)
            {
                int cnt = 0;
                for (int k = 0; k < CandidateCount && cnt < GreedyChoices; k++)
                {
                    int c = cand[cur * CandidateCount + k];
                    if (!visited[c])
                        pick[cnt++] = c;
                }

                int next;
                if (cnt > 0)
                {
                    next = pick[rng.Next(cnt)];
                }
                else
                {
                    next = -1;
                    int bestD = int.MaxValue;
                    for (int c = 0; c < N; c++)
                    {
                        if (!visited[c] && dist[cur * N + c] < bestD)
                        {
                            bestD = dist[cur * N + c];
                            next = c;
                        }
                    }
                }

                order[step] = next;
                visited[next] = true;
                cur = next;
            }

            return new Tour { Order = order, Length = TourLength(order) };
        }

        // =============== Пошук фуражирами на одній ділянці ===============
        static void ImproveSite(Tour site, int foragers, int[] pos, int[] tmp, int[] bestNeighbour)
        {
            int[] o = site.Order;
            for (int i = 0; i < N; i++)
                pos[o[i]] = i;

            int bestLen = site.Length;
            bool found = false;

            for (int f = 0; f < foragers; f++)
            {
                MakeNeighbour(o, pos, tmp);
                int len = TourLength(tmp);
                if (len < bestLen)
                {
                    bestLen = len;
                    Array.Copy(tmp, bestNeighbour, N);
                    found = true;
                }
            }

            if (found)
            {
                Array.Copy(bestNeighbour, site.Order, N);
                site.Length = bestLen;
            }
        }

        // Локальний крок: вибираємо місто a (позиція i) і кандидата b з його списку найближчих,
        // після чого прагнемо отримати ребро a -> b одним із трьох способів.
        //   ~50%: 2-opt (розворот дуги між a та b)
        //   ~40%: переміщення міста b одразу після a
        //   ~10%: обмін місцями міст a і b (диверсифікація)
        static void MakeNeighbour(int[] o, int[] pos, int[] tmp)
        {
            int i = rng.Next(N);
            int a = o[i];
            int b = cand[a * CandidateCount + rng.Next(CandidateCount)];
            int j = pos[b];

            double r = rng.NextDouble();

            if (r < 0.5)
            {
                // 2-opt: розвертаємо циклічну дугу з позицій i+1 .. j
                Array.Copy(o, tmp, N);
                int s = (i + 1) % N;
                int len = (j - i + N) % N;
                for (int k = 0; k < len / 2; k++)
                {
                    int p = (s + k) % N;
                    int q = (s + len - 1 - k) % N;
                    int t = tmp[p];
                    tmp[p] = tmp[q];
                    tmp[q] = t;
                }
            }
            else if (r < 0.9)
            {
                // Переміщення міста b на позицію одразу після a
                int w = 0;
                for (int k = 0; k < N; k++)
                {
                    if (k == j) continue;
                    tmp[w++] = o[k];
                    if (k == i)
                        tmp[w++] = b;
                }
            }
            else
            {
                // Обмін двох міст
                Array.Copy(o, tmp, N);
                tmp[i] = b;
                tmp[j] = a;
            }
        }

        // =============== Допоміжні функції ===============
        static int TourLength(int[] order)
        {
            int sum = 0;
            for (int i = 0; i < N - 1; i++)
                sum += dist[order[i] * N + order[i + 1]];
            sum += dist[order[N - 1] * N + order[0]];
            return sum;
        }

        static Tour CloneTour(Tour t)
        {
            return new Tour { Order = (int[])t.Order.Clone(), Length = t.Length };
        }
    }
}