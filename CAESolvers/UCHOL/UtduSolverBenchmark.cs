namespace CAESolvers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Замер производительности прямого решателя <see cref="SymmetricUtduSolver"/>
    /// на задаче того масштаба, для которого он и предназначен — порядка
    /// 300 000 уравнений.
    ///
    /// Задача берётся синтетическая, но с той же структурой разреженности, что
    /// у настоящей трёхмерной задачи МКЭ: регулярная сетка, в каждом узле
    /// несколько степеней свободы, связь соседних узлов — заполненный блок.
    /// Именно структура определяет и заполнение множителя, и форму дерева
    /// исключений, и достижимое ускорение по ядрам, поэтому такой замер
    /// переносим на реальные расчётные схемы; абсолютные значения на матрице
    /// той же размерности, но, скажем, оболочечной, будут другими — там
    /// заполнение существенно меньше.
    ///
    /// Что здесь измеряется по отдельности и почему это важно:
    /// <list type="bullet">
    /// <item>символьная фаза — выполняется один раз на структуру;</item>
    /// <item>численная факторизация при разном числе потоков — единственная
    /// действительно дорогая часть, ради неё и сделано распараллеливание;</item>
    /// <item>решение по готовому множителю — стоимость каждого следующего
    /// варианта загружения.</item>
    /// </list>
    /// Соотношение этих трёх величин и определяет, как правильно встраивать
    /// решатель в расчёт.
    /// </summary>
    public static class UtduSolverBenchmark
    {
        /// <summary>
        /// Выполняет замер.
        /// </summary>
        /// <param name="targetEquations">Желаемое число уравнений (округляется до целой сетки).</param>
        /// <param name="degreesOfFreedom">Число степеней свободы в узле (3 — трёхмерная упругость).</param>
        /// <param name="threadCounts">
        /// Числа потоков для замера. null — от одного до числа логических ядер
        /// с удвоением. Замер с одним потоком нужен для оценки ускорения, но на
        /// задаче в 300 000 уравнений он самый долгий.
        /// </param>
        /// <param name="output">Куда писать отчёт; null — в консоль.</param>
        public static void Run(
            int targetEquations = 300_000,
            int degreesOfFreedom = 3,
            int[]? threadCounts = null,
            TextWriter? output = null)
        {
            var report = output ?? Console.Out;
            threadCounts ??= DefaultThreadCounts();

            int side = Math.Max(2, (int)Math.Round(Math.Cbrt((double)targetEquations / degreesOfFreedom)));

            report.WriteLine("=== Прямой решатель U^T D U: замер производительности ===");
            report.WriteLine($"Машина: {Environment.ProcessorCount} логических ядер, " +
                             $"векторный регистр {System.Numerics.Vector<double>.Count} x double, " +
                             $"64-битный процесс: {Environment.Is64BitProcess}");
            report.WriteLine();

            var assemblyTimer = Stopwatch.StartNew();
            var matrix = BuildGridMatrix(side, side, side, degreesOfFreedom);
            assemblyTimer.Stop();

            report.WriteLine($"Задача: сетка {side}x{side}x{side}, {degreesOfFreedom} ст. свободы в узле");
            report.WriteLine($"  уравнений:                {matrix.Size,15:N0}");
            report.WriteLine($"  ненулевых (верх. треуг.): {matrix.NonZeroCount,15:N0}");
            report.WriteLine($"  сборка матрицы:           {assemblyTimer.Elapsed.TotalSeconds,15:F2} с");
            report.WriteLine();

            // --- Символьная фаза ---
            var symbolicTimer = Stopwatch.StartNew();
            var solver = new SymmetricUtduSolver();
            var symbolic = solver.Analyze(matrix);
            symbolicTimer.Stop();

            report.WriteLine("Символьная фаза (AMD + дерево исключений + суперузлы):");
            report.WriteLine($"  время:                    {symbolicTimer.Elapsed.TotalSeconds,15:F2} с");
            report.WriteLine($"  ненулевых в множителе:    {symbolic.StrictFactorNonZeroCount,15:N0}");
            report.WriteLine($"  хранится (с блочными 0):  {symbolic.FactorEntryCount,15:N0}" +
                             $"   (+{100.0 * symbolic.FactorEntryCount / symbolic.StrictFactorNonZeroCount - 100.0:F1}%)");
            report.WriteLine($"  заполнение к матрице:     {(double)symbolic.StrictFactorNonZeroCount / matrix.NonZeroCount,15:F1} раз");
            report.WriteLine($"  суперузлов:               {symbolic.SupernodeCount,15:N0}" +
                             $"   (в среднем {(double)symbolic.Size / symbolic.SupernodeCount:F1} столбцов)");
            report.WriteLine($"  наибольший фронт:         {symbolic.MaxFrontSize,15:N0}" +
                             $"   ({Megabytes(symbolic.MaxFrontBytes):N0} МБ)");
            report.WriteLine($"  операций факторизации:    {symbolic.FactorOperationCount,15:N0}");
            report.WriteLine();
            report.WriteLine("Память:");
            report.WriteLine($"  множитель:                {Megabytes(symbolic.EstimatedFactorBytes),15:N0} МБ");
            report.WriteLine($"  символьные структуры:     {Megabytes(symbolic.StructureBytes),15:N0} МБ");
            report.WriteLine($"  матрицы вкладов (пик):    {Megabytes(symbolic.PeakContributionBytes),15:N0} МБ");

            foreach (int threads in threadCounts)
            {
                report.WriteLine($"  оценка пика, {threads,3} потока(ов):{Megabytes(symbolic.EstimatePeakBytes(threads)),13:N0} МБ");
            }

            report.WriteLine("  Примечание: фактический рабочий набор процесса обычно на 15-25% больше");
            report.WriteLine("  оценки — сборщик мусора не сразу возвращает системе память под временные");
            report.WriteLine("  фронтальные матрицы. Если оценка близка к объёму ОЗУ, уменьшите число");
            report.WriteLine("  потоков: каждый поток удерживает свой буфер фронта.");
            report.WriteLine();

            // --- Численная факторизация ---
            var rightHandSide = BuildRightHandSide(matrix.Size);
            double baselineSeconds = 0.0;

            report.WriteLine("Численная факторизация:");
            report.WriteLine($"  {"потоков",8} {"время, с",12} {"Гфлопс",10} {"ускорение",11} {"эффект-ть",11} {"пик ОЗУ, МБ",13}");

            foreach (int threads in threadCounts)
            {
                // Перед каждым замером память освобождается, иначе пик
                // предыдущего прогона исказит картину.
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

                var options = new UtduSolverOptions { MaxDegreeOfParallelism = threads };
                var threadedSolver = new SymmetricUtduSolver(options);

                var timer = Stopwatch.StartNew();
                var factorization = threadedSolver.Factorize(matrix, symbolic);
                timer.Stop();

                double seconds = timer.Elapsed.TotalSeconds;
                if (baselineSeconds == 0.0)
                    baselineSeconds = seconds;

                double speedup = baselineSeconds / seconds;
                double gigaflops = symbolic.FactorOperationCount / seconds / 1e9;
                long peak = Process.GetCurrentProcess().PeakWorkingSet64;

                report.WriteLine($"  {threads,8} {seconds,12:F2} {gigaflops,10:F2} {speedup,10:F2}x " +
                                 $"{100.0 * speedup / threads,10:F0}% {Megabytes(peak),13:N0}");

                if (factorization.RegularizedPivotCount > 0)
                {
                    report.WriteLine($"    ВНИМАНИЕ: регуляризовано ведущих элементов: " +
                                     $"{factorization.RegularizedPivotCount}, из них отрицательных: " +
                                     $"{factorization.NegativePivotCount}");
                }

                // Решение и проверка — на последнем (самом быстром) прогоне.
                if (threads == threadCounts[^1])
                    ReportSolvePhase(report, matrix, factorization, rightHandSide);
            }

            report.WriteLine();
            report.WriteLine("Как это читать. Символьная фаза выполняется один раз на структуру матрицы:");
            report.WriteLine("в нелинейном расчёте её результат нужно сохранять и передавать в Factorize.");
            report.WriteLine("Каждое следующее решение по готовому множителю стоит в десятки-сотни раз");
            report.WriteLine("меньше самой факторизации, и тем меньше, чем крупнее задача — именно поэтому");
            report.WriteLine("прямой метод выгоден при многих вариантах загружения.");
            report.WriteLine("Эффективность распараллеливания падает с ростом числа");
            report.WriteLine("потоков закономерно: у корня дерева исключений независимой работы почти не");
            report.WriteLine("остаётся, и там решатель опирается на параллелизм внутри одного фронта.");
            report.Flush();
        }

        private static void ReportSolvePhase(
            TextWriter report, SymmetricCSRMatrix matrix,
            UtduNumericFactorization factorization, double[] rightHandSide)
        {
            var timer = Stopwatch.StartNew();
            var solution = factorization.Solve(rightHandSide);
            timer.Stop();
            double firstSolve = timer.Elapsed.TotalMilliseconds;

            timer.Restart();
            for (int repeat = 0; repeat < 5; repeat++)
                factorization.Solve(rightHandSide);
            timer.Stop();
            double perSolve = timer.Elapsed.TotalMilliseconds / 5.0;

            double norm = Math.Sqrt(rightHandSide.Sum(v => v * v));
            double residual = UtduNumericFactorization.ResidualNorm(matrix, rightHandSide, solution);

            report.WriteLine();
            report.WriteLine("Решение по готовому множителю:");
            report.WriteLine($"  первое решение:           {firstSolve,15:F0} мс");
            report.WriteLine($"  среднее по 5 повторам:    {perSolve,15:F0} мс");
            report.WriteLine($"  относительная невязка:    {residual / norm,15:E3}");
            report.WriteLine($"  диапазон диагонали D:     {factorization.SmallestPivotMagnitude:E2} .. " +
                             $"{factorization.LargestPivotMagnitude:E2}" +
                             $"  (отношение {factorization.LargestPivotMagnitude / factorization.SmallestPivotMagnitude:E2})");
            report.WriteLine($"  положительно определена:  {factorization.IsPositiveDefinite,15}");
        }

        private static int[] DefaultThreadCounts()
        {
            var counts = new System.Collections.Generic.List<int>();
            for (int threads = 1; threads < Environment.ProcessorCount; threads *= 2)
                counts.Add(threads);

            counts.Add(Environment.ProcessorCount);
            return counts.Distinct().ToArray();
        }

        private static long Megabytes(long bytes) => bytes / (1024 * 1024);

        private static double[] BuildRightHandSide(int size)
        {
            // Детерминированная правая часть: замеры должны быть повторяемыми.
            var random = new Random(20260812);
            var vector = new double[size];
            for (int i = 0; i < size; i++)
                vector[i] = random.NextDouble() * 2.0 - 1.0;

            return vector;
        }

        /// <summary>
        /// Собирает матрицу со структурой разреженности трёхмерной задачи МКЭ
        /// на регулярной сетке. Значения выбраны так, чтобы диагональ была
        /// доминирующей: матрица получается заведомо положительно определённой,
        /// и замер не смешивается с вопросами обусловленности.
        /// </summary>
        public static SymmetricCSRMatrix BuildGridMatrix(int nx, int ny, int nz, int degreesOfFreedom)
        {
            int nodes = nx * ny * nz;
            var builder = new SymmetricCSRMatrixBuilder(nodes * degreesOfFreedom);
            var neighbourCount = new int[nodes];

            int Index(int x, int y, int z) => (z * ny + y) * nx + x;

            void Couple(int a, int b)
            {
                for (int d = 0; d < degreesOfFreedom; d++)
                    for (int e = 0; e < degreesOfFreedom; e++)
                        builder.AddToElement(a * degreesOfFreedom + d, b * degreesOfFreedom + e, d == e ? -1.0 : -0.0625);

                neighbourCount[a]++;
                neighbourCount[b]++;
            }

            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                    {
                        int node = Index(x, y, z);
                        if (x + 1 < nx) Couple(node, Index(x + 1, y, z));
                        if (y + 1 < ny) Couple(node, Index(x, y + 1, z));
                        if (z + 1 < nz) Couple(node, Index(x, y, z + 1));
                    }

            for (int node = 0; node < nodes; node++)
                for (int d = 0; d < degreesOfFreedom; d++)
                {
                    for (int e = d + 1; e < degreesOfFreedom; e++)
                        builder.AddToElement(node * degreesOfFreedom + d, node * degreesOfFreedom + e, -0.25);

                    builder.AddToElement(
                        node * degreesOfFreedom + d,
                        node * degreesOfFreedom + d,
                        neighbourCount[node] * degreesOfFreedom + 0.25 * (degreesOfFreedom - 1) + 1.0);
                }

            return builder.Build();
        }
    }
}
