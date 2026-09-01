using System;
using System.Linq;
using CAESolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Тесты прямого решателя SymmetricUtduSolver (суперузловая
    /// мультифронтальная факторизация A = U^T D U).
    ///
    /// Проверки построены на трёх независимых источниках истины, потому что
    /// одной невязки для прямого решателя недостаточно: она мала и у решения
    /// слегка не той системы.
    /// <list type="number">
    /// <item>задачи с известным точным решением (диагональные и собранные как
    /// b = A * x_ожидаемое);</item>
    /// <item>сравнение с независимым решателем — плотным методом Гаусса с
    /// выбором ведущего элемента и с уже имеющимся ConjugateGradientGaussPreSolver;</item>
    /// <item>инварианты, которые обязаны выполняться при любых настройках:
    /// результат не должен зависеть ни от переупорядочивания, ни от числа
    /// потоков.</item>
    /// </list>
    /// Схожие сценарии объединены в один [DataTestMethod] с несколькими
    /// [DataRow], как и в остальных тестах проекта.
    /// </summary>
    [TestClass]
    public class SymmetricUtduSolverTests
    {
        // --- Построение тестовых матриц -----------------------------------

        private static SymmetricCSRMatrix BuildDiagonalMatrix(double[] diagonal)
        {
            var builder = new SymmetricCSRMatrixBuilder(diagonal.Length);
            for (int i = 0; i < diagonal.Length; i++)
                builder.AddToElement(i, i, diagonal[i]);

            return builder.Build();
        }

        private static SymmetricCSRMatrix BuildTridiagonalMatrix(int n, double diagonal, double offDiagonal)
        {
            var builder = new SymmetricCSRMatrixBuilder(n);
            for (int i = 0; i < n; i++)
                builder.AddToElement(i, i, diagonal);

            for (int i = 0; i < n - 1; i++)
                builder.AddToElement(i, i + 1, offDiagonal);

            return builder.Build();
        }

        /// <summary>
        /// Матрица, по структуре повторяющая глобальную матрицу жёсткости МКЭ
        /// на регулярной трёхмерной сетке: каждый узел связан с соседями по
        /// трём направлениям, в узле dof степеней свободы, связь узлов —
        /// заполненный блок dof x dof. Диагональ делается доминирующей, что
        /// гарантирует положительную определённость и позволяет сравнивать
        /// результат с эталоном без оговорок про обусловленность.
        /// </summary>
        private static SymmetricCSRMatrix BuildGridMatrix(int nx, int ny, int nz, int dof)
        {
            int nodes = nx * ny * nz;
            var builder = new SymmetricCSRMatrixBuilder(nodes * dof);
            var neighbourCount = new int[nodes];

            int Index(int x, int y, int z) => (z * ny + y) * nx + x;

            void Couple(int a, int b)
            {
                for (int d = 0; d < dof; d++)
                    for (int e = 0; e < dof; e++)
                        builder.AddToElement(a * dof + d, b * dof + e, d == e ? -1.0 : -0.0625);

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
                for (int d = 0; d < dof; d++)
                {
                    for (int e = d + 1; e < dof; e++)
                        builder.AddToElement(node * dof + d, node * dof + e, -0.25);

                    builder.AddToElement(
                        node * dof + d,
                        node * dof + d,
                        neighbourCount[node] * dof + 0.25 * (dof - 1) + 1.0);
                }

            return builder.Build();
        }

        /// <summary>
        /// Разреженная симметричная матрица со случайной структурой и строго
        /// доминирующей диагональю (а значит, положительно определённая).
        /// Случайная структура важна: у регулярных сеток дерево исключений
        /// правильной формы, и часть возможных ошибок символьной фазы на них
        /// просто не проявляется.
        /// </summary>
        private static SymmetricCSRMatrix BuildRandomMatrix(int n, double density, int seed)
        {
            var random = new Random(seed);
            var builder = new SymmetricCSRMatrixBuilder(n);
            var offDiagonalSum = new double[n];

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    if (random.NextDouble() >= density)
                        continue;

                    double value = random.NextDouble() * 2.0 - 1.0;
                    builder.AddToElement(i, j, value);
                    offDiagonalSum[i] += Math.Abs(value);
                    offDiagonalSum[j] += Math.Abs(value);
                }

            for (int i = 0; i < n; i++)
                builder.AddToElement(i, i, offDiagonalSum[i] + 1.0);

            return builder.Build();
        }

        private static double[] BuildVector(int n, int seed)
        {
            var random = new Random(seed);
            var vector = new double[n];
            for (int i = 0; i < n; i++)
                vector[i] = random.NextDouble() * 2.0 - 1.0;

            return vector;
        }

        // --- Эталонное решение --------------------------------------------

        /// <summary>
        /// Независимый эталон: плотный метод Гаусса с выбором ведущего
        /// элемента по столбцу. Не разделяет с проверяемым решателем ни
        /// перестановок, ни структуры данных, ни порядка операций, поэтому
        /// совпадение результатов — содержательная проверка, а не тавтология.
        /// Применим только к небольшим матрицам (стоимость O(n^3) и память O(n^2)).
        /// </summary>
        private static double[] SolveDense(SymmetricCSRMatrix matrix, double[] rightHandSide)
        {
            int n = matrix.Size;
            var a = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    a[i, j] = matrix[i, j];

            var b = (double[])rightHandSide.Clone();

            for (int k = 0; k < n; k++)
            {
                int pivot = k;
                for (int i = k + 1; i < n; i++)
                    if (Math.Abs(a[i, k]) > Math.Abs(a[pivot, k]))
                        pivot = i;

                if (pivot != k)
                {
                    for (int j = 0; j < n; j++)
                        (a[k, j], a[pivot, j]) = (a[pivot, j], a[k, j]);

                    (b[k], b[pivot]) = (b[pivot], b[k]);
                }

                for (int i = k + 1; i < n; i++)
                {
                    double factor = a[i, k] / a[k, k];
                    if (factor == 0.0)
                        continue;

                    for (int j = k; j < n; j++)
                        a[i, j] -= factor * a[k, j];

                    b[i] -= factor * b[k];
                }
            }

            var x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = b[i];
                for (int j = i + 1; j < n; j++)
                    sum -= a[i, j] * x[j];

                x[i] = sum / a[i, i];
            }

            return x;
        }

        private static double RelativeResidual(SymmetricCSRMatrix matrix, double[] b, double[] x)
        {
            double normB = Math.Sqrt(b.Sum(v => v * v));
            double residual = UtduNumericFactorization.ResidualNorm(matrix, b, x);

            return normB > 0.0 ? residual / normB : residual;
        }

        private static double MaxRelativeDifference(double[] actual, double[] expected)
        {
            double scale = Math.Max(1e-300, expected.Max(Math.Abs));

            double worst = 0.0;
            for (int i = 0; i < actual.Length; i++)
                worst = Math.Max(worst, Math.Abs(actual[i] - expected[i]) / scale);

            return worst;
        }

        // --- Задачи с точно известным решением -----------------------------

        /// <summary>
        /// Диагональные системы: решение известно точно и покомпонентно, а
        /// множитель обязан выйти единичным. Заодно проверяются вырожденные по
        /// размеру случаи — одно уравнение и пустая система, — на которых
        /// обычно и ломаются граничные условия циклов символьной фазы.
        /// </summary>
        [DataTestMethod]
        [DataRow(new[] { 2.0, 4.0, 5.0 }, new[] { 3.0, 1.0, 2.0 }, DisplayName = "Диагональная система из трёх уравнений")]
        [DataRow(new[] { 4.0 }, new[] { 2.0 }, DisplayName = "Одно уравнение")]
        [DataRow(new[] { 1e-6, 1e6 }, new[] { 1.0, -1.0 }, DisplayName = "Диагональ с разбросом в 12 порядков")]
        public void Solve_DiagonalSystem_ReproducesExactSolution(double[] diagonal, double[] expectedSolution)
        {
            var matrix = BuildDiagonalMatrix(diagonal);
            var b = matrix.Multiply(expectedSolution);

            var solution = new SymmetricUtduSolver().Solve(new LinearSystem(matrix, b));

            for (int i = 0; i < expectedSolution.Length; i++)
                Assert.AreEqual(expectedSolution[i], solution[i], 1e-9 * Math.Abs(expectedSolution[i]) + 1e-12);
        }

        /// <summary>Пустая система — допустимый вход, а не особый случай.</summary>
        [TestMethod]
        public void Solve_EmptySystem_ReturnsEmptySolution()
        {
            var matrix = new SymmetricCSRMatrixBuilder(0).Build();

            var solution = new SymmetricUtduSolver().Solve(new LinearSystem(matrix, Array.Empty<double>()));

            Assert.AreEqual(0, solution.Length);
        }

        /// <summary>
        /// Нулевая правая часть должна давать в точности нулевое решение —
        /// без «почти нуля» от накопленной погрешности.
        /// </summary>
        [TestMethod]
        public void Solve_ZeroRightHandSide_ReturnsExactZero()
        {
            var matrix = BuildGridMatrix(5, 5, 5, 2);

            var solution = new SymmetricUtduSolver().Solve(new LinearSystem(matrix, new double[matrix.Size]));

            CollectionAssert.AreEqual(new double[matrix.Size], solution);
        }

        // --- Сравнение с независимым эталоном ------------------------------

        /// <summary>
        /// Совпадение с плотным методом Гаусса на матрицах разной плотности и
        /// при обоих алгоритмах переупорядочивания. Плотность 0.9 отдельно
        /// важна тем, что даёт один широкий суперузел, а 0.02 — множество
        /// мелких: это два принципиально разных пути в суперузловом разбиении.
        /// </summary>
        [DataTestMethod]
        [DataRow(40, 0.02, FillReducingOrdering.ApproximateMinimumDegree, DisplayName = "Очень разреженная, AMD")]
        [DataRow(40, 0.02, FillReducingOrdering.Natural, DisplayName = "Очень разреженная, без переупорядочивания")]
        [DataRow(55, 0.15, FillReducingOrdering.ApproximateMinimumDegree, DisplayName = "Разреженная, AMD")]
        [DataRow(55, 0.15, FillReducingOrdering.Natural, DisplayName = "Разреженная, без переупорядочивания")]
        [DataRow(45, 0.90, FillReducingOrdering.ApproximateMinimumDegree, DisplayName = "Почти плотная, AMD")]
        [DataRow(45, 0.90, FillReducingOrdering.Natural, DisplayName = "Почти плотная, без переупорядочивания")]
        public void Solve_RandomSparseSystem_MatchesDenseGaussianElimination(
            int size, double density, FillReducingOrdering ordering)
        {
            var matrix = BuildRandomMatrix(size, density, seed: size * 31 + (int)(density * 1000));
            var b = BuildVector(size, seed: 7);

            var solver = new SymmetricUtduSolver(new UtduSolverOptions { Ordering = ordering });
            var solution = solver.Solve(new LinearSystem(matrix, b));
            var expected = SolveDense(matrix, b);

            Assert.IsTrue(MaxRelativeDifference(solution, expected) < 1e-9,
                $"Решение расходится с эталоном: {MaxRelativeDifference(solution, expected):E3}");
            Assert.IsTrue(RelativeResidual(matrix, b, solution) < 1e-12,
                $"Относительная невязка слишком велика: {RelativeResidual(matrix, b, solution):E3}");
        }

        /// <summary>
        /// Совпадение с уже имеющимся в проекте итерационным решателем на
        /// МКЭ-подобной трёхмерной задаче. Два метода не имеют между собой
        /// ничего общего, кроме самой матрицы, поэтому согласие результатов
        /// проверяет их обоих сразу.
        /// </summary>
        [TestMethod]
        public void Solve_GridSystem_AgreesWithConjugateGradientGaussPreSolver()
        {
            var matrix = BuildGridMatrix(7, 7, 7, 2);
            var b = BuildVector(matrix.Size, seed: 11);

            var system = new LinearSystem(matrix, b);
            var direct = new SymmetricUtduSolver().Solve(system);
            var iterativeSolver = new ConjugateGradientGaussPreSolver
            {
                RelativeTolerance = 1e-12,
                MaxIterations = 20000
            };
            var iterative = iterativeSolver.Solve(system);

            Assert.IsTrue(iterativeSolver.LastResult!.Converged,
                "Итерационный решатель не сошёлся — сравнивать не с чем");
            Assert.IsTrue(MaxRelativeDifference(direct, iterative) < 1e-6,
                $"Прямое и итерационное решения расходятся: {MaxRelativeDifference(direct, iterative):E3}");
        }

        /// <summary>
        /// Задачи разной структуры — цепочка, «звезда» с одной очень плотной
        /// строкой, тонкая пластина и трёхмерный блок. «Звезда» проверяет
        /// отдельную ветку в AMD (вершины аномально большой степени
        /// исключаются последними), а пластина и блок дают деревья исключений
        /// совершенно разной формы.
        /// </summary>
        [DataTestMethod]
        [DataRow(0, DisplayName = "Трёхдиагональная цепочка из 400 уравнений")]
        [DataRow(1, DisplayName = "Звезда: одна плотная строка")]
        [DataRow(2, DisplayName = "Тонкая пластина 15x15x2")]
        [DataRow(3, DisplayName = "Трёхмерный блок 8x8x8, 3 степени свободы в узле")]
        public void Factorize_VariousStructures_SolvesAccuratelyAndDetectsPositiveDefiniteness(int scenario)
        {
            SymmetricCSRMatrix matrix;
            switch (scenario)
            {
                case 0:
                    matrix = BuildTridiagonalMatrix(400, diagonal: 4.0, offDiagonal: -1.0);
                    break;
                case 1:
                    var builder = new SymmetricCSRMatrixBuilder(300);
                    for (int i = 1; i < 300; i++)
                        builder.AddToElement(0, i, -1.0);
                    builder.AddToElement(0, 0, 301.0);
                    for (int i = 1; i < 300; i++)
                        builder.AddToElement(i, i, 2.0);
                    matrix = builder.Build();
                    break;
                case 2:
                    matrix = BuildGridMatrix(15, 15, 2, 2);
                    break;
                default:
                    matrix = BuildGridMatrix(8, 8, 8, 3);
                    break;
            }

            var b = BuildVector(matrix.Size, seed: scenario + 3);

            var factorization = new SymmetricUtduSolver().Factorize(matrix);
            var solution = factorization.Solve(b);

            Assert.IsTrue(RelativeResidual(matrix, b, solution) < 1e-10,
                $"Относительная невязка: {RelativeResidual(matrix, b, solution):E3}");
            Assert.IsTrue(factorization.IsPositiveDefinite,
                "Диагонально доминантная матрица должна пройти факторизацию как положительно определённая");
            Assert.AreEqual(0, factorization.RegularizedPivotCount);
        }

        // --- Инварианты ----------------------------------------------------

        /// <summary>
        /// Результат не должен зависеть от числа потоков. Это главная защита
        /// от ошибок распараллеливания: гонка в сборке фронтов или в порядке
        /// обхода дерева проявилась бы именно здесь, причём порог 1e-12
        /// намеренно жёсткий — обход дерева при любом числе потоков
        /// математически один и тот же, и расхождения быть не должно даже на
        /// уровне порядка суммирования.
        /// </summary>
        [DataTestMethod]
        [DataRow(2, DisplayName = "2 потока")]
        [DataRow(3, DisplayName = "3 потока")]
        [DataRow(8, DisplayName = "8 потоков")]
        [DataRow(32, DisplayName = "32 потока (заведомо больше числа ядер)")]
        public void Factorize_AnyDegreeOfParallelism_MatchesSerialResult(int threads)
        {
            var matrix = BuildGridMatrix(9, 9, 9, 2);
            var b = BuildVector(matrix.Size, seed: 17);

            var serial = new SymmetricUtduSolver(new UtduSolverOptions { MaxDegreeOfParallelism = 1 })
                .Factorize(matrix).Solve(b);
            var parallel = new SymmetricUtduSolver(new UtduSolverOptions { MaxDegreeOfParallelism = threads })
                .Factorize(matrix).Solve(b);

            Assert.IsTrue(MaxRelativeDifference(parallel, serial) < 1e-12,
                $"Решение зависит от числа потоков: расхождение {MaxRelativeDifference(parallel, serial):E3}");
        }

        /// <summary>
        /// Переупорядочивание меняет порядок операций, но не решение.
        /// </summary>
        [TestMethod]
        public void Factorize_OrderingChoice_DoesNotChangeSolution()
        {
            var matrix = BuildGridMatrix(6, 6, 6, 2);
            var b = BuildVector(matrix.Size, seed: 23);

            var natural = new SymmetricUtduSolver(new UtduSolverOptions
            {
                Ordering = FillReducingOrdering.Natural
            }).Solve(new LinearSystem(matrix, b));

            var reordered = new SymmetricUtduSolver(new UtduSolverOptions
            {
                Ordering = FillReducingOrdering.ApproximateMinimumDegree
            }).Solve(new LinearSystem(matrix, b));

            Assert.IsTrue(MaxRelativeDifference(reordered, natural) < 1e-9,
                $"Решения при разных перестановках расходятся: {MaxRelativeDifference(reordered, natural):E3}");
        }

        /// <summary>
        /// Смысл AMD — снижение заполнения, и это проверяемое утверждение, а не
        /// декларация: на трёхмерной задаче множитель должен получиться
        /// заметно короче, чем в исходном порядке. Порог взят с большим
        /// запасом (фактический выигрыш здесь — в разы), чтобы тест не стал
        /// хрупким при доработках эвристик.
        /// </summary>
        [TestMethod]
        public void Analyze_ApproximateMinimumDegree_ReducesFillComparedToNaturalOrder()
        {
            var matrix = BuildGridMatrix(12, 12, 12, 1);

            var natural = new SymmetricUtduSolver(new UtduSolverOptions
            {
                Ordering = FillReducingOrdering.Natural
            }).Analyze(matrix);

            var reordered = new SymmetricUtduSolver(new UtduSolverOptions
            {
                Ordering = FillReducingOrdering.ApproximateMinimumDegree
            }).Analyze(matrix);

            Assert.IsTrue(reordered.StrictFactorNonZeroCount * 2 < natural.StrictFactorNonZeroCount,
                $"AMD не снизил заполнение: {reordered.StrictFactorNonZeroCount:N0} против " +
                $"{natural.StrictFactorNonZeroCount:N0} в исходном порядке");

            // Блочное хранение суперузлов добавляет явные нули, но их доля
            // должна оставаться небольшой — иначе объединение суперузлов
            // работает слишком агрессивно.
            Assert.IsTrue(reordered.FactorEntryCount < reordered.StrictFactorNonZeroCount * 1.2,
                "Слишком много явных нулей в блочном хранении множителя");
        }

        /// <summary>
        /// Перестановка, выдаваемая AMD, обязана быть перестановкой: любая
        /// потерянная или задвоенная неизвестная сделала бы решение
        /// бессмысленным, и поймать это лучше здесь, а не по невязке.
        /// </summary>
        [DataTestMethod]
        [DataRow(0, DisplayName = "Трёхмерный блок")]
        [DataRow(1, DisplayName = "Случайная разреженная структура")]
        [DataRow(2, DisplayName = "Матрица без внедиагональных элементов")]
        [DataRow(3, DisplayName = "Две несвязанные подсистемы")]
        public void ApproximateMinimumDegree_ProducesValidPermutation(int scenario)
        {
            SymmetricCSRMatrix matrix;
            switch (scenario)
            {
                case 0:
                    matrix = BuildGridMatrix(7, 6, 5, 2);
                    break;
                case 1:
                    matrix = BuildRandomMatrix(250, 0.02, seed: 99);
                    break;
                case 2:
                    matrix = BuildDiagonalMatrix(Enumerable.Range(1, 60).Select(i => (double)i).ToArray());
                    break;
                default:
                    var builder = new SymmetricCSRMatrixBuilder(200);
                    for (int i = 0; i < 99; i++)
                        builder.AddToElement(i, i + 1, -1.0);
                    for (int i = 100; i < 199; i++)
                        builder.AddToElement(i, i + 1, -1.0);
                    for (int i = 0; i < 200; i++)
                        builder.AddToElement(i, i, 4.0);
                    matrix = builder.Build();
                    break;
            }

            var graph = SymmetricPatternGraph.FromMatrix(matrix);
            var permutation = ApproximateMinimumDegreeOrdering.Compute(graph);

            Assert.AreEqual(matrix.Size, permutation.Length);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, matrix.Size).ToArray(), permutation);
        }

        // --- Переиспользование факторизации --------------------------------

        /// <summary>
        /// Смысл прямого решателя — считать множитель один раз и решать по
        /// нему сколько угодно правых частей. Проверяется, что повторные
        /// вызовы Solve по одному множителю независимы и все дают верный
        /// результат.
        /// </summary>
        [TestMethod]
        public void Solve_MultipleRightHandSidesOnOneFactorization_AllCorrect()
        {
            var matrix = BuildGridMatrix(8, 8, 6, 2);
            var factorization = new SymmetricUtduSolver().Factorize(matrix);

            for (int variant = 0; variant < 5; variant++)
            {
                var b = BuildVector(matrix.Size, seed: 100 + variant);
                var solution = factorization.Solve(b);

                Assert.IsTrue(RelativeResidual(matrix, b, solution) < 1e-10,
                    $"Правая часть №{variant}: невязка {RelativeResidual(matrix, b, solution):E3}");
            }
        }

        /// <summary>
        /// Сценарий нелинейного расчёта: структура матрицы жёсткости от
        /// итерации к итерации не меняется, меняются только значения, поэтому
        /// символьная фаза должна выполняться один раз. Проверяется, что
        /// повторная факторизация с переданной символьной фазой даёт верный
        /// результат для уже изменённой матрицы, — то есть что в символьной
        /// фазе действительно не осталось ни одного числа из матрицы.
        /// </summary>
        [TestMethod]
        public void Factorize_ReusedSymbolicAfterValuesChanged_StillSolvesCorrectly()
        {
            var matrix = BuildGridMatrix(8, 8, 5, 2);
            var solver = new SymmetricUtduSolver();
            var symbolic = solver.Analyze(matrix);
            var b = BuildVector(matrix.Size, seed: 31);

            var first = solver.Factorize(matrix, symbolic).Solve(b);
            Assert.IsTrue(RelativeResidual(matrix, b, first) < 1e-10);

            for (int i = 0; i < matrix.Size; i++)
                matrix.AccumulateAt(i, i, 5.0);

            var second = solver.Factorize(matrix, symbolic).Solve(b);

            Assert.IsTrue(RelativeResidual(matrix, b, second) < 1e-10,
                $"После изменения значений невязка {RelativeResidual(matrix, b, second):E3}");
            Assert.IsTrue(MaxRelativeDifference(second, first) > 1e-6,
                "Решение не изменилось — значит, изменение матрицы не было учтено");
        }

        /// <summary>
        /// Символьную факторизацию нельзя переиспользовать для матрицы с
        /// другой структурой: молча посчитать по чужой структуре означало бы
        /// выдать неверный результат без единого признака ошибки.
        /// </summary>
        [TestMethod]
        public void Factorize_SymbolicFromDifferentStructure_Throws()
        {
            var solver = new SymmetricUtduSolver();
            var symbolic = solver.Analyze(BuildTridiagonalMatrix(100, 4.0, -1.0));

            Assert.ThrowsException<ArgumentException>(
                () => solver.Factorize(BuildGridMatrix(5, 5, 4, 1), symbolic));
        }

        /// <summary>
        /// Несовпадение длины вектора правой части с размером матрицы —
        /// ошибка вызывающего кода, а не повод посчитать что-нибудь.
        /// </summary>
        [DataTestMethod]
        [DataRow(3, DisplayName = "Вектор короче матрицы")]
        [DataRow(12, DisplayName = "Вектор длиннее матрицы")]
        public void Solve_RightHandSideLengthMismatch_Throws(int length)
        {
            var matrix = BuildTridiagonalMatrix(8, 4.0, -1.0);
            var solver = new SymmetricUtduSolver();

            Assert.ThrowsException<ArgumentException>(() => new LinearSystem(matrix, new double[length]));
            Assert.ThrowsException<ArgumentException>(() => solver.Factorize(matrix).Solve(new double[length]));
        }

        // --- Не положительно определённые матрицы --------------------------

        /// <summary>
        /// Вырожденная матрица: блок [[1,1],[1,1]] не имеет обратной. По
        /// умолчанию факторизация доводится до конца с регуляризацией
        /// диагонали, но обязана об этом сообщить — иначе расчётчик получит
        /// решение возмущённой задачи, не подозревая об этом. При запрещённой
        /// регуляризации должно быть исключение.
        /// </summary>
        [TestMethod]
        public void Factorize_SingularMatrix_ReportsRegularizationOrThrows()
        {
            var builder = new SymmetricCSRMatrixBuilder(4);
            builder.AddToElement(0, 0, 1.0);
            builder.AddToElement(1, 1, 1.0);
            builder.AddToElement(0, 1, 1.0);
            builder.AddToElement(2, 2, 2.0);
            builder.AddToElement(3, 3, 3.0);
            var matrix = builder.Build();

            var permissive = new SymmetricUtduSolver(new UtduSolverOptions
            {
                AllowDiagonalRegularization = true
            }).Factorize(matrix);

            Assert.IsTrue(permissive.RegularizedPivotCount > 0,
                "Вырожденность матрицы должна быть замечена и сосчитана");
            Assert.IsFalse(permissive.IsPositiveDefinite);

            var strict = new SymmetricUtduSolver(new UtduSolverOptions
            {
                AllowDiagonalRegularization = false
            });

            Assert.ThrowsException<InvalidOperationException>(() => strict.Factorize(matrix));
        }

        /// <summary>
        /// Знаконеопределённая матрица (отрицательное собственное значение)
        /// должна быть распознана по отрицательному ведущему элементу, а не
        /// «успешно» разложена.
        /// </summary>
        [TestMethod]
        public void Factorize_IndefiniteMatrix_ReportsNegativePivot()
        {
            var builder = new SymmetricCSRMatrixBuilder(3);
            builder.AddToElement(0, 0, 1.0);
            builder.AddToElement(1, 1, 1.0);
            builder.AddToElement(2, 2, 1.0);
            builder.AddToElement(0, 1, 3.0);   // делает матрицу знаконеопределённой
            var matrix = builder.Build();

            var factorization = new SymmetricUtduSolver(new UtduSolverOptions
            {
                AllowDiagonalRegularization = true
            }).Factorize(matrix);

            Assert.IsTrue(factorization.NegativePivotCount > 0,
                "Отрицательный ведущий элемент должен быть замечен");
            Assert.IsFalse(factorization.IsPositiveDefinite);
        }

        // --- Символьные оценки ---------------------------------------------

        /// <summary>
        /// Символьная фаза должна давать пригодные для планирования оценки:
        /// заполнение, объём памяти и число операций. Проверяется их взаимная
        /// согласованность и то, что оценка пиковой памяти не меньше памяти
        /// под сам множитель (иначе она бесполезна как ограничение сверху).
        /// </summary>
        [TestMethod]
        public void Analyze_ProvidesConsistentCostEstimates()
        {
            var matrix = BuildGridMatrix(10, 10, 10, 2);
            var symbolic = new SymmetricUtduSolver().Analyze(matrix);

            Assert.AreEqual(matrix.Size, symbolic.Size);
            Assert.IsTrue(symbolic.SupernodeCount > 0);
            Assert.IsTrue(symbolic.StrictFactorNonZeroCount >= matrix.NonZeroCount,
                "Множитель не может быть короче исходной матрицы");
            Assert.IsTrue(symbolic.FactorEntryCount >= symbolic.StrictFactorNonZeroCount,
                "Блочное хранение не может занимать меньше, чем есть ненулевых элементов");
            Assert.AreEqual(symbolic.FactorEntryCount * sizeof(double), symbolic.EstimatedFactorBytes);
            Assert.IsTrue(symbolic.EstimatePeakBytes(4) > symbolic.EstimatedFactorBytes,
                "Пиковая память включает множитель и рабочие массивы, значит строго больше");
            Assert.IsTrue(symbolic.FactorOperationCount > symbolic.StrictFactorNonZeroCount,
                "Число операций факторизации должно превышать число элементов множителя");
            Assert.IsTrue(symbolic.MaxFrontSize > 0 && symbolic.MaxFrontSize <= matrix.Size);

            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, matrix.Size).ToArray(), symbolic.Permutation);

            for (int k = 0; k < symbolic.Size; k++)
                Assert.AreEqual(k, symbolic.InversePermutation[symbolic.Permutation[k]]);
        }
    }
}
