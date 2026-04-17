using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Trasa_MB
{
    class Program
    {
        static void Main(string[] args)
        {
            string filePath = "dystanse.txt";
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Błąd: Nie znaleziono pliku dystanse.txt w folderze bin/Debug!");
                return;
            }

            try
            {
                // 1. WCZYTYWANIE I PARSOWANIE DANYCH
                var (names, matrix) = ParseTspFile(filePath);
                int n = names.Count;
                int startIdx = names.FindIndex(x => x.Equals("BielskoBiala", StringComparison.OrdinalIgnoreCase));
                if (startIdx == -1) startIdx = 0;

                Console.WriteLine($"Wczytano {n} miast. Punkt startowy: {names[startIdx]}\n");

                // Przygotowanie tras do porównania
                // Dla AEX i HGreX potrzebujemy dwóch "rodziców". 
                // Stworzymy ich używając NN z dwóch różnych miast.
                var p1 = GetNearestNeighborRoute(matrix, startIdx);
                var p2 = GetNearestNeighborRoute(matrix, (startIdx + 10) % n);

                // --- OBLICZENIA ---

                // A. Najbliższy Sąsiad 
                var nnRoute = GetNearestNeighborRoute(matrix, startIdx);
                var nnOpt = Apply2Opt(nnRoute, matrix);

                // B. AEX 
                var aexRoute = CrossAEX(p1, p2, matrix);
                var aexOpt = Apply2Opt(aexRoute, matrix);

                // C. HGreX 
                var hgrexRoute = CrossHGREX(p1, p2, matrix);
                var hgrexOpt = Apply2Opt(hgrexRoute, matrix);

                // Wyniki
                PrintResult("Najbliższy Sąsiad", nnRoute, nnOpt, matrix);
                PrintResult("AEX", aexRoute, aexOpt, matrix);
                PrintResult("HGreX", hgrexRoute, hgrexOpt, matrix);

                Console.WriteLine("\nSzczegółowa trasa Najlepsza (HGreX + 2-opt):");
                Console.WriteLine(string.Join(" -> ", hgrexOpt.Select(i => names[i])));

                Console.WriteLine("\nNaciśnij dowolny klawisz, aby zakończyć...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wystąpił krytyczny błąd: {ex.Message}");
            }
        }

        static (List<string> names, int[,] matrix) ParseTspFile(string path)
        {
            string raw = File.ReadAllText(path);
            // Usuwanie tagów 
            StringBuilder sb = new StringBuilder();
            bool inTag = false;
            foreach (char c in raw)
            {
                if (c == '[') inTag = true;
                if (!inTag) sb.Append(c);
                if (c == ']') inTag = false;
            }

            string[] tokens = sb.ToString().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> cityNames = new List<string>();
            List<int> distances = new List<int>();

            foreach (var token in tokens)
            {
                if (int.TryParse(token, out int d)) distances.Add(d);
                else cityNames.Add(token);
            }

            int n = cityNames.Count;
            int[,] m = new int[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    m[i, j] = distances[i * n + j];

            return (cityNames, m);
        }

        // --- ALGORYTMY ---

        // Najbliższego sąsiada
        static List<int> GetNearestNeighborRoute(int[,] matrix, int start)
        {
            int n = matrix.GetLength(0);
            List<int> route = new List<int> { start };
            bool[] visited = new bool[n];
            visited[start] = true;

            for (int i = 0; i < n - 1; i++)
            {
                int curr = route.Last();
                int next = -1, min = int.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (!visited[j] && matrix[curr, j] < min)
                    {
                        min = matrix[curr, j];
                        next = j;
                    }
                }
                route.Add(next);
                visited[next] = true;
            }
            route.Add(start);
            return route;
        }

        // 2-OPT 
        static List<int> Apply2Opt(List<int> route, int[,] matrix)
        {
            var best = new List<int>(route);
            bool improved = true;
            while (improved)
            {
                improved = false;
                for (int i = 1; i < best.Count - 2; i++)
                {
                    for (int j = i + 1; j < best.Count - 1; j++)
                    {
                        int currentDist = matrix[best[i - 1], best[i]] + matrix[best[j], best[j + 1]];
                        int newDist = matrix[best[i - 1], best[j]] + matrix[best[i], best[j + 1]];

                        if (newDist < currentDist)
                        {
                            best.Reverse(i, j - i + 1);
                            improved = true;
                        }
                    }
                }
            }
            return best;
        }

        // AEX 
        static List<int> CrossAEX(List<int> p1, List<int> p2, int[,] matrix)
        {
            int n = matrix.GetLength(0);
            List<int> child = new List<int> { p1[0] };
            bool[] visited = new bool[n];
            visited[p1[0]] = true;

            for (int i = 0; i < n - 1; i++)
            {
                int curr = child.Last();
                var parent = (i % 2 == 0) ? p1 : p2;
                int idx = parent.IndexOf(curr);
                int next = parent[idx + 1];

                if (visited[next]) // Jeśli zajęte, bierzemy najbliższe wolne
                {
                    int bestNext = -1, min = int.MaxValue;
                    for (int j = 0; j < n; j++)
                        if (!visited[j] && matrix[curr, j] < min) { min = matrix[curr, j]; bestNext = j; }
                    next = bestNext;
                }
                child.Add(next);
                visited[next] = true;
            }
            child.Add(child[0]);
            return child;
        }

        // HGreX 
        static List<int> CrossHGREX(List<int> p1, List<int> p2, int[,] matrix)
        {
            int n = matrix.GetLength(0);
            List<int> child = new List<int> { p1[0] };
            bool[] visited = new bool[n];
            visited[p1[0]] = true;

            for (int i = 0; i < n - 1; i++)
            {
                int curr = child.Last();
                int n1 = p1[p1.IndexOf(curr) + 1];
                int n2 = p2[p2.IndexOf(curr) + 1];

                int next;
                if (!visited[n1] && !visited[n2]) next = matrix[curr, n1] < matrix[curr, n2] ? n1 : n2;
                else if (!visited[n1]) next = n1;
                else if (!visited[n2]) next = n2;
                else // Oba zajęte -> najbliższy sąsiad z całej reszty
                {
                    int best = -1, min = int.MaxValue;
                    for (int j = 0; j < n; j++)
                        if (!visited[j] && matrix[curr, j] < min) { min = matrix[curr, j]; best = j; }
                    next = best;
                }
                child.Add(next);
                visited[next] = true;
            }
            child.Add(child[0]);
            return child;
        }

      
        static int CalcDist(List<int> r, int[,] m)
        {
            int d = 0;
            for (int i = 0; i < r.Count - 1; i++) d += m[r[i], r[i + 1]];
            return d;
        }

        static void PrintResult(string label, List<int> raw, List<int> opt, int[,] m)
        {
            Console.WriteLine($"{label.PadRight(25)} | Przed 2-opt: {CalcDist(raw, m)} km | Po 2-opt: {CalcDist(opt, m)} km");
        }
    }
}