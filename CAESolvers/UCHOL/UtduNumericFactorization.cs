namespace CAESolvers
{
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Численная факторизация A = U^T D U симметричной матрицы: единичная
    /// верхняя треугольная U и диагональная D. Хранится по строкам U, что для
    /// матрицы, заданной верхним треугольником, естественно и совпадает со
    /// столбцовым хранением L = U^T (математически это то же самое разложение
    /// L D L^T).
    ///
    /// Объект содержит готовый множитель и позволяет решать систему с любым
    /// числом правых частей за O(nnz(U)) на каждую — то есть на порядки
    /// дешевле самой факторизации. Именно в этом главное преимущество прямого
    /// решателя перед итерационным для задач с многими вариантами загружения:
    /// дорогая работа делается один раз.
    /// </summary>
    public sealed class UtduNumericFactorization
    {
        private readonly UtduSymbolicFactorization symbolic;
        private readonly double[][] slabs;
        private readonly double[] diagonal;

        internal UtduNumericFactorization(
            UtduSymbolicFactorization symbolic, double[][] slabs, double[] diagonal,
            int regularizedPivotCount, int negativePivotCount,
            double smallestPivotMagnitude, double largestPivotMagnitude)
        {
            this.symbolic = symbolic;
            this.slabs = slabs;
            this.diagonal = diagonal;

            RegularizedPivotCount = regularizedPivotCount;
            NegativePivotCount = negativePivotCount;
            SmallestPivotMagnitude = smallestPivotMagnitude;
            LargestPivotMagnitude = largestPivotMagnitude;
        }

        /// <summary>Символьная факторизация, на которой построен множитель.</summary>
        public UtduSymbolicFactorization Symbolic => symbolic;

        /// <summary>Число уравнений.</summary>
        public int Size => symbolic.Size;

        /// <summary>
        /// Сколько ведущих элементов пришлось заменить (регуляризовать).
        /// Ноль означает, что матрица прошла факторизацию как положительно
        /// определённая. Ненулевое значение — расчётная схема близка к
        /// вырожденной, и решение относится к слегка возмущённой задаче.
        /// </summary>
        public int RegularizedPivotCount { get; }

        /// <summary>
        /// Сколько ведущих элементов оказались отрицательными. Для настоящей
        /// положительно определённой матрицы это невозможно, поэтому
        /// ненулевое значение — верный признак потери устойчивости расчётной
        /// схемы, а не проблемы численной точности.
        /// </summary>
        public int NegativePivotCount { get; }

        /// <summary>Наименьший по модулю элемент D.</summary>
        public double SmallestPivotMagnitude { get; }

        /// <summary>Наибольший по модулю элемент D.</summary>
        public double LargestPivotMagnitude { get; }

        /// <summary>
        /// true, если факторизация прошла без регуляризации и все элементы D
        /// положительны, то есть матрица подтверждённо положительно определена.
        /// </summary>
        public bool IsPositiveDefinite => RegularizedPivotCount == 0 && NegativePivotCount == 0;

        /// <summary>
        /// Диагональ D в порядке исключения. Отношение
        /// LargestPivotMagnitude / SmallestPivotMagnitude — дешёвая (хотя и
        /// грубая снизу) оценка обусловленности задачи.
        /// </summary>
        public ReadOnlySpan<double> Diagonal => diagonal;

        /// <summary>
        /// Решает A x = b по готовому множителю: прямая подстановка U^T y = b,
        /// деление на диагональ и обратная подстановка U x = D^-1 y.
        /// </summary>
        public double[] Solve(double[] rightHandSide)
        {
            if (rightHandSide == null)
                throw new ArgumentNullException(nameof(rightHandSide));

            int n = Size;
            if (rightHandSide.Length != n)
                throw new ArgumentException(
                    $"Размер вектора правой части {rightHandSide.Length} не соответствует размеру матрицы {n}");

            var permutation = symbolic.Permutation;
            var working = new double[n];
            for (int i = 0; i < n; i++)
                working[i] = rightHandSide[permutation[i]];

            SolvePermuted(working);

            var solution = new double[n];
            for (int i = 0; i < n; i++)
                solution[permutation[i]] = working[i];

            return solution;
        }

        /// <summary>
        /// Решает систему для вектора, уже записанного в порядке исключения,
        /// с результатом на том же месте.
        /// </summary>
        private void SolvePermuted(double[] working)
        {
            var supernodes = symbolic.Supernodes;
            var scratch = new double[Math.Max(1, supernodes.MaxFrontSize)];

            ForwardSubstitution(working, scratch);

            for (int i = 0; i < working.Length; i++)
                working[i] /= diagonal[i];

            BackwardSubstitution(working, scratch);
        }

        /// <summary>
        /// Прямая подстановка U^T y = b. Суперузлы обходятся по возрастанию:
        /// строки «подвала» суперузла всегда больше его столбцов, поэтому к
        /// моменту обработки суперузла все вклады в его столбцы уже внесены.
        /// Значения собираются в непрерывный буфер и возвращаются обратно
        /// одним проходом — иначе рассеянный доступ к вектору в самом
        /// внутреннем цикле съел бы весь выигрыш от плотного хранения.
        /// </summary>
        private void ForwardSubstitution(double[] working, double[] scratch)
        {
            var supernodes = symbolic.Supernodes;
            var patternRows = supernodes.PatternRows;
            var layout = symbolic.Layout;

            for (int s = 0; s < supernodes.Count; s++)
            {
                int frontSize = supernodes.FrontSize(s);
                int width = supernodes.Width(s);
                int patternStart = supernodes.PatternPointers[s];
                var block = slabs[layout.SlabIndex[s]];

                for (int t = 0; t < frontSize; t++)
                    scratch[t] = working[patternRows[patternStart + t]];

                int offset = layout.SlabOffset[s];
                for (int k = 0; k < width; k++)
                {
                    int columnLength = frontSize - k;
                    double value = scratch[k];

                    if (value != 0.0)
                    {
                        // scratch[k+t] -= U[k, ...] * value для t = 1..columnLength-1
                        DenseKernels.SubtractScaled(scratch, k + 1, block, offset + 1, columnLength - 1, value);
                    }

                    offset += columnLength;
                }

                for (int t = 0; t < frontSize; t++)
                    working[patternRows[patternStart + t]] = scratch[t];
            }
        }

        /// <summary>
        /// Обратная подстановка U x = z. Суперузлы обходятся по убыванию:
        /// значения в строках «подвала» к этому моменту уже окончательные.
        /// </summary>
        private void BackwardSubstitution(double[] working, double[] scratch)
        {
            var supernodes = symbolic.Supernodes;
            var patternRows = supernodes.PatternRows;
            var layout = symbolic.Layout;

            for (int s = supernodes.Count - 1; s >= 0; s--)
            {
                int frontSize = supernodes.FrontSize(s);
                int width = supernodes.Width(s);
                int patternStart = supernodes.PatternPointers[s];
                var block = slabs[layout.SlabIndex[s]];
                int blockBase = layout.SlabOffset[s];

                for (int t = 0; t < frontSize; t++)
                    scratch[t] = working[patternRows[patternStart + t]];

                for (int k = width - 1; k >= 0; k--)
                {
                    int offset = blockBase + ColumnOffset(k, frontSize);
                    int columnLength = frontSize - k;

                    scratch[k] -= DenseKernels.Dot(block, offset + 1, scratch, k + 1, columnLength - 1);
                }

                for (int t = 0; t < width; t++)
                    working[patternRows[patternStart + t]] = scratch[t];
            }
        }

        /// <summary>
        /// Смещение столбца k в упакованном нижнем треугольнике фронта
        /// размера frontSize: столбец k занимает frontSize - k позиций.
        /// </summary>
        internal static int ColumnOffset(int column, int frontSize) =>
            column * frontSize - column * (column - 1) / 2;

        /// <summary>
        /// Вычисляет невязку ||b - A x|| — дешёвая и обязательная проверка
        /// после прямого решения: она стоит одно умножение матрицы на вектор и
        /// сразу показывает, не потеряна ли точность из-за плохой
        /// обусловленности или регуляризации.
        /// </summary>
        public static double ResidualNorm(SymmetricCSRMatrix matrix, double[] rightHandSide, double[] solution)
        {
            var product = matrix.Multiply(solution);
            double sum = 0.0;

            for (int i = 0; i < rightHandSide.Length; i++)
            {
                double difference = rightHandSide[i] - product[i];
                sum += difference * difference;
            }

            return Math.Sqrt(sum);
        }
    }

    /// <summary>
    /// Суперузловая мультифронтальная численная факторизация.
    ///
    /// Схема. Каждому суперузлу отвечает «фронтальная матрица» — небольшая
    /// плотная симметричная матрица, в которую собираются: элементы исходной
    /// матрицы из столбцов суперузла и матрицы вкладов (дополнения Шура) всех
    /// его потомков по дереву. После сборки первые несколько столбцов фронта
    /// факторизуются и уходят в множитель, а оставшийся блок становится
    /// матрицей вкладов уже для родителя. Так вся факторизация превращается в
    /// обход дерева снизу вверх, где вся арифметика — плотная.
    ///
    /// Распараллеливание. Суперузел готов к обработке, когда обработаны все
    /// его потомки, поэтому независимые поддеревья считаются одновременно.
    /// Планировщик держит очередь готовых суперузлов и счётчик необработанных
    /// потомков: как только счётчик родителя обнуляется, родитель попадает в
    /// очередь. Ближе к корню дерево сужается и независимой работы не
    /// остаётся — там включается второй уровень параллелизма, внутри
    /// блочного обновления одного (уже большого) фронта. Вместе это даёт
    /// загрузку ядер и на «широком низе», и на «узком верху» дерева.
    /// </summary>
    internal static class SupernodalMultifrontalFactorizer
    {
        /// <summary>
        /// Наибольший размер фронтальной матрицы (в элементах), буфер под
        /// которую поток удерживает для переиспользования. 2^25 элементов —
        /// это 256 МБ на поток в худшем случае.
        /// </summary>
        internal const int FrontRetentionLimit = 1 << 25;

        /// <summary>
        /// Наибольший размер матрицы вкладов (в элементах), которая берётся из
        /// пула вместо отдельного выделения; 2^23 элементов — это 64 МБ.
        ///
        /// Матриц вкладов столько же, сколько суперузлов — десятки тысяч, — и
        /// каждая живёт лишь до сборки родителя. Без переиспользования этот
        /// поток недолговечных крупных объектов заставляет сборщик мусора
        /// раздувать кучу: полезных данных при этом столько же, но процесс
        /// занимает в полтора-два раза больше памяти. Поэтому все вклады до
        /// указанного размера (а это подавляющее большинство) переиспользуются,
        /// а более крупные выделяются отдельно, чтобы пул не удерживал их до
        /// конца расчёта.
        /// </summary>
        private const int ContributionPoolLimit = 1 << 23;

        public static UtduNumericFactorization Factorize(
            UtduSymbolicFactorization symbolic, SymmetricCSRMatrix matrix, UtduSolverOptions options)
        {
            var supernodes = symbolic.Supernodes;
            int supernodeCount = supernodes.Count;
            int n = symbolic.Size;

            var updates = new double[]?[Math.Max(1, supernodeCount)];
            var diagonal = new double[n];

            if (supernodeCount == 0)
                return new UtduNumericFactorization(symbolic, Array.Empty<double[]>(), diagonal, 0, 0, 0.0, 0.0);

            // Вся память под множитель запрашивается здесь, до первой операции:
            // если её не хватает, узнать об этом лучше сразу.
            var slabs = symbolic.Layout.Allocate();

            long maxFrontLength = (long)supernodes.MaxFrontSize * (supernodes.MaxFrontSize + 1) / 2;
            if (maxFrontLength > int.MaxValue)
                throw new InvalidOperationException(
                    $"Наибольшая фронтальная матрица ({supernodes.MaxFrontSize} x {supernodes.MaxFrontSize}) " +
                    "не представима одним массивом. Задача слишком плотная для прямого решателя в этой " +
                    "постановке — проверьте расчётную схему на наличие уравнений, связывающих слишком " +
                    "много неизвестных.");

            var contributionPool = ArrayPool<double>.Create(
                ContributionPoolLimit, Math.Clamp(options.ResolveWorkerCount(), 2, 8));

            double scale = ComputeDiagonalScale(matrix);
            int workerCount = Math.Min(options.ResolveWorkerCount(), supernodeCount);
            int panelWidth = options.ResolvePanelWidth();

            var remainingChildren = new int[supernodeCount];
            var ready = new ConcurrentStack<int>();
            int initialReady = 0;

            for (int s = 0; s < supernodeCount; s++)
            {
                remainingChildren[s] = supernodes.ChildPointers[s + 1] - supernodes.ChildPointers[s];
                if (remainingChildren[s] == 0)
                {
                    ready.Push(s);
                    initialReady++;
                }
            }

            var contexts = new WorkerContext[workerCount];
            var schedule = new Schedule(ready, remainingChildren, supernodeCount, initialReady, workerCount);

            void RunWorker(int workerIndex)
            {
                var context = new WorkerContext(n, supernodes.MaxFrontSize, panelWidth);
                contexts[workerIndex] = context;

                try
                {
                    var values = matrix.Values;

                    while (schedule.TryTake(out int supernode))
                    {
                        schedule.EnterWork();
                        try
                        {
                            int innerDegree = Math.Max(1, workerCount - schedule.ActiveWorkers + 1);
                            FactorizeSupernode(
                                supernode, symbolic, values, slabs, updates, diagonal,
                                context, contributionPool, options, scale, panelWidth, innerDegree);
                        }
                        finally
                        {
                            schedule.LeaveWork();
                        }

                        schedule.Complete(supernode, supernodes.Parent[supernode]);
                    }
                }
                catch (Exception exception)
                {
                    // Ошибку нельзя просто выбросить: остальные потоки ждут
                    // работы на семафоре и без явной остановки не проснутся.
                    schedule.Fail(exception);
                }
            }

            if (workerCount == 1)
            {
                RunWorker(0);
            }
            else
            {
                var tasks = new Task[workerCount];
                for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
                {
                    int captured = workerIndex;
                    tasks[workerIndex] = Task.Run(() => RunWorker(captured));
                }

                Task.WaitAll(tasks);
            }

            schedule.ThrowIfFailed();

            int regularized = 0;
            int negative = 0;
            double smallest = double.MaxValue;
            double largest = 0.0;

            foreach (var context in contexts)
            {
                if (context == null)
                    continue;

                regularized += context.RegularizedCount;
                negative += context.NegativeCount;
                smallest = Math.Min(smallest, context.SmallestPivot);
                largest = Math.Max(largest, context.LargestPivot);
            }

            if (smallest == double.MaxValue)
                smallest = 0.0;

            return new UtduNumericFactorization(symbolic, slabs, diagonal, regularized, negative, smallest, largest);
        }

        /// <summary>
        /// Масштаб матрицы для оценки «малости» ведущего элемента. Берётся
        /// максимум модуля диагонали: для матриц жёсткости это естественная
        /// единица измерения, и порог, выраженный в её долях, не зависит от
        /// системы единиц расчёта.
        /// </summary>
        private static double ComputeDiagonalScale(SymmetricCSRMatrix matrix)
        {
            double scale = 0.0;
            for (int i = 0; i < matrix.Size; i++)
                scale = Math.Max(scale, Math.Abs(matrix.GetDiagonal(i)));

            return scale > 0.0 ? scale : 1.0;
        }

        private static void FactorizeSupernode(
            int supernode, UtduSymbolicFactorization symbolic, ReadOnlySpan<double> values,
            double[][] slabs, double[]?[] updates, double[] diagonal,
            WorkerContext context, ArrayPool<double> contributionPool, UtduSolverOptions options,
            double scale, int panelWidth, int innerDegree)
        {
            var supernodes = symbolic.Supernodes;
            var pattern = symbolic.Pattern;
            var patternRows = supernodes.PatternRows;

            int firstColumn = supernodes.FirstColumn[supernode];
            int width = supernodes.Width(supernode);
            int frontSize = supernodes.FrontSize(supernode);
            int patternStart = supernodes.PatternPointers[supernode];

            int frontLength = (int)((long)frontSize * (frontSize + 1) / 2);

            var offsets = context.ColumnOffsets;
            offsets[0] = 0;
            for (int j = 0; j < frontSize; j++)
                offsets[j + 1] = offsets[j] + (frontSize - j);

            var front = context.EnsureFront(frontLength);
            Array.Clear(front, 0, frontLength);

            var rowMap = context.RowMap;
            for (int t = 0; t < frontSize; t++)
                rowMap[patternRows[patternStart + t]] = t;

            // --- Сборка: элементы исходной матрицы ---
            for (int local = 0; local < width; local++)
            {
                int column = firstColumn + local;

                int diagonalIndex = pattern.DiagonalValueIndices[column];
                if (diagonalIndex >= 0)
                    front[offsets[local]] += values[diagonalIndex];

                int lowerBase = pattern.LowerValuePointers[column];
                int start = pattern.LowerStart[column];
                int end = pattern.Pointers[column + 1];
                int columnBase = offsets[local] - local;

                for (int p = start; p < end; p++)
                {
                    int row = rowMap[pattern.Rows[p]];
                    front[columnBase + row] += values[pattern.LowerValueIndices[lowerBase + (p - start)]];
                }
            }

            // --- Сборка: матрицы вкладов потомков ---
            int childEnd = supernodes.ChildPointers[supernode + 1];
            for (int c = supernodes.ChildPointers[supernode]; c < childEnd; c++)
            {
                int child = supernodes.Children[c];
                var update = updates[child];
                if (update == null)
                    continue;

                int childWidth = supernodes.Width(child);
                int childPatternStart = supernodes.PatternPointers[child];
                int trailing = supernodes.FrontSize(child) - childWidth;

                // Отображение строк «подвала» потомка в локальные индексы
                // родителя. Оба списка отсортированы, поэтому отображение
                // монотонно и треугольная структура вклада сохраняется.
                var relative = context.RelativeIndex;
                for (int a = 0; a < trailing; a++)
                    relative[a] = rowMap[patternRows[childPatternStart + childWidth + a]];

                int source = 0;
                for (int a = 0; a < trailing; a++)
                {
                    int targetColumn = relative[a];
                    int targetBase = offsets[targetColumn] - targetColumn;

                    for (int b = a; b < trailing; b++)
                        front[targetBase + relative[b]] += update[source + (b - a)];

                    source += trailing - a;
                }

                // Вклад потомка больше не нужен: ссылка сбрасывается сразу,
                // чтобы память освободилась, не дожидаясь конца факторизации.
                // На больших задачах именно суммарный объём ещё не поглощённых
                // вкладов определяет пиковую память.
                updates[child] = null;
                if (update.Length <= ContributionPoolLimit)
                    contributionPool.Return(update);
            }

            // --- Факторизация фронта ---
            FactorizeFront(
                front, offsets, frontSize, width, firstColumn, diagonal,
                context, options, scale, panelWidth, innerDegree);

            // --- Множитель и матрица вкладов для родителя ---
            int blockLength = offsets[width];
            var layout = symbolic.Layout;
            Array.Copy(front, 0, slabs[layout.SlabIndex[supernode]], layout.SlabOffset[supernode], blockLength);

            int remainingRows = frontSize - width;
            if (remainingRows > 0)
            {
                int updateLength = frontLength - blockLength;
                var contribution = updateLength <= ContributionPoolLimit
                    ? contributionPool.Rent(updateLength)
                    : GC.AllocateUninitializedArray<double>(updateLength);
                Array.Copy(front, blockLength, contribution, 0, updateLength);
                updates[supernode] = contribution;
            }
        }

        /// <summary>
        /// Блочная правосторонняя факторизация фронта. Пивоты обрабатываются
        /// панелями: внутри панели идёт обычное исключение (её столбцы к концу
        /// панели полностью готовы), а всё влияние панели на остальную часть
        /// фронта вносится одним блочным обновлением. Такая организация
        /// заменяет множество проходов по всему фронту одним и переводит
        /// арифметику в режим, ограниченный не памятью, а вычислениями.
        /// </summary>
        private static void FactorizeFront(
            double[] front, int[] offsets, int frontSize, int width, int firstColumn,
            double[] diagonal, WorkerContext context, UtduSolverOptions options,
            double scale, int panelWidth, int innerDegree)
        {
            double pivotThreshold = options.PivotTolerance * scale;
            double regularizationValue = options.DiagonalRegularization * scale;

            for (int panelStart = 0; panelStart < width; panelStart += panelWidth)
            {
                int panelEnd = Math.Min(panelStart + panelWidth, width);

                for (int k = panelStart; k < panelEnd; k++)
                {
                    int columnOffset = offsets[k];
                    int columnLength = frontSize - k;
                    double pivot = front[columnOffset];

                    if (!(pivot > pivotThreshold))
                    {
                        if (!options.AllowDiagonalRegularization)
                            throw new InvalidOperationException(
                                $"Ведущий элемент {pivot:E3} в уравнении {firstColumn + k} (в порядке исключения) " +
                                "не положителен: матрица не является положительно определённой. " +
                                "Разрешите регуляризацию диагонали, если требуется довести факторизацию до конца.");

                        if (pivot < 0.0)
                            context.NegativeCount++;

                        context.RegularizedCount++;
                        pivot = regularizationValue;
                    }

                    diagonal[firstColumn + k] = pivot;
                    context.Observe(pivot);

                    DenseKernels.Scale(front, columnOffset + 1, columnLength - 1, 1.0 / pivot);

                    // Влияние пивота k на остальные столбцы панели.
                    for (int j = k + 1; j < panelEnd; j++)
                    {
                        double factor = pivot * front[columnOffset + (j - k)];
                        if (factor == 0.0)
                            continue;

                        DenseKernels.SubtractScaled(
                            front, offsets[j], front, columnOffset + (j - k), frontSize - j, factor);
                    }

                    // В хранимом множителе диагональ U единичная.
                    front[columnOffset] = 1.0;
                }

                if (panelEnd < frontSize)
                {
                    ApplyPanelUpdate(
                        front, offsets, frontSize, panelStart, panelEnd, firstColumn,
                        diagonal, context.PanelBuffer, innerDegree, options.ParallelUpdateThreshold);
                }
            }
        }

        /// <summary>
        /// Вносит вклад готовой панели во всю оставшуюся часть фронта:
        /// F[i,j] -= sum_k U[k,i] * D[k] * U[k,j]. Панель предварительно
        /// умножается на D и перекладывается построчно в буфер, который
        /// умещается в кеш второго уровня, — тогда самый внутренний цикл
        /// становится скалярным произведением двух непрерывных участков.
        /// Столбцы независимы, поэтому обновление распараллеливается без
        /// синхронизации; это и есть второй уровень параллелизма, работающий
        /// у корня дерева.
        /// </summary>
        /// <summary>
        /// Высота блока строк, на которые разбивается «подвал» фронта. Блок
        /// панели размером RowBlock * PanelWidth должен помещаться в кеш
        /// первого уровня: тогда, пока по нему проходят все столбцы блока, он
        /// читается из кеша, а не из памяти. При ширине панели 32 это 32 КБ.
        /// </summary>
        internal const int RowBlock = 128;

        /// <summary>Число столбцов результата, обновляемых одновременно.</summary>
        private const int ColumnTile = 4;

        private static void ApplyPanelUpdate(
            double[] front, int[] offsets, int frontSize, int panelStart, int panelEnd,
            int firstColumn, double[] diagonal, double[] buffer, int innerDegree, long parallelThreshold)
        {
            int panelWidth = panelEnd - panelStart;
            int trailing = frontSize - panelEnd;
            int blockCount = (trailing + RowBlock - 1) / RowBlock;

            // Панель умножается на диагональ и укладывается блоками строк.
            // Внутри блока — по столбцам панели с фиксированным шагом RowBlock,
            // поэтому в горячем цикле подряд идущие строки лежат подряд в
            // памяти и читаются одной векторной инструкцией, а весь блок
            // остаётся в кеше на всё время обработки своих столбцов.
            for (int block = 0; block < blockCount; block++)
            {
                int blockBase = block * (RowBlock * panelWidth);
                int blockFirstRow = panelEnd + block * RowBlock;
                int blockRows = Math.Min(RowBlock, frontSize - blockFirstRow);

                for (int kk = 0; kk < panelWidth; kk++)
                {
                    int column = panelStart + kk;
                    int source = offsets[column] + (blockFirstRow - column);
                    double pivot = diagonal[firstColumn + column];
                    int destination = blockBase + kk * RowBlock;

                    for (int r = 0; r < blockRows; r++)
                        buffer[destination + r] = front[source + r] * pivot;

                    // Хвост блока обнуляется, чтобы векторный цикл мог
                    // читать блок целиком, не проверяя границу на каждом шаге.
                    for (int r = blockRows; r < RowBlock; r++)
                        buffer[destination + r] = 0.0;
                }
            }

            long work = (long)trailing * trailing * panelWidth;

            if (innerDegree > 1 && work > parallelThreshold)
            {
                int chunkCount = Math.Min(trailing, innerDegree * 8);
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = innerDegree };

                Parallel.For(0, chunkCount, parallelOptions, chunk =>
                {
                    int from = panelEnd + (int)((long)trailing * chunk / chunkCount);
                    int to = panelEnd + (int)((long)trailing * (chunk + 1) / chunkCount);
                    UpdateTrailingColumns(front, offsets, frontSize, panelStart, panelEnd, buffer, from, to);
                });
            }
            else
            {
                UpdateTrailingColumns(front, offsets, frontSize, panelStart, panelEnd, buffer, panelEnd, frontSize);
            }
        }

        /// <summary>
        /// Обновляет заданный диапазон столбцов «подвала» фронта вкладом
        /// готовой панели. Столбцы обрабатываются группами по
        /// <see cref="ColumnTile"/>: накопители результата (по одному
        /// векторному регистру на столбец группы) живут в регистрах на всё
        /// время прохода по панели, поэтому каждое чтение панели даёт сразу
        /// ColumnTile умножений-сложений, а результат пишется в память один
        /// раз на группу. Векторизация идёт по строкам — по тому измерению,
        /// вдоль которого фронт непрерывен, — так что здесь нет ни
        /// горизонтальных сумм, ни зависимости по накопителю, которые
        /// ограничивали бы темп вычислений.
        /// </summary>
        private static void UpdateTrailingColumns(
            double[] front, int[] offsets, int frontSize, int panelStart, int panelEnd,
            double[] buffer, int columnFrom, int columnTo)
        {
            int panelWidth = panelEnd - panelStart;
            int blockCount = (frontSize - panelEnd + RowBlock - 1) / RowBlock;

            // Значения панели для текущей группы столбцов: tile[k * ColumnTile + t].
            var tile = new double[panelWidth * ColumnTile];

            for (int block = 0; block < blockCount; block++)
            {
                int blockBase = block * (RowBlock * panelWidth);
                int blockFirstRow = panelEnd + block * RowBlock;
                int blockLastRow = Math.Min(blockFirstRow + RowBlock, frontSize);

                // В нижнем треугольнике строка i участвует только при i >= j,
                // поэтому в этом блоке строк осмысленны лишь столбцы j < blockLastRow.
                int columnLimit = Math.Min(columnTo, blockLastRow);

                int j = columnFrom;
                for (; j + ColumnTile - 1 < columnLimit; j += ColumnTile)
                {
                    GatherPanelTile(front, offsets, panelStart, panelWidth, j, ColumnTile, tile);

                    // Строки, где активны все столбцы группы.
                    int fullFrom = Math.Max(j + ColumnTile - 1, blockFirstRow);
                    UpdateColumnTile(front, offsets, buffer, blockBase, blockFirstRow,
                        fullFrom, blockLastRow, panelWidth, j, tile);

                    // Треугольный «уголок»: не более ColumnTile-1 строк, где
                    // часть столбцов группы ещё не задействована.
                    for (int i = Math.Max(j, blockFirstRow); i < fullFrom; i++)
                    {
                        for (int t = 0; t <= i - j; t++)
                        {
                            front[offsets[j + t] + (i - j - t)] -= PanelDot(
                                buffer, blockBase + (i - blockFirstRow), panelWidth, tile, t);
                        }
                    }
                }

                for (; j < columnLimit; j++)
                {
                    GatherPanelTile(front, offsets, panelStart, panelWidth, j, 1, tile);

                    int offsetJ = offsets[j];
                    for (int i = Math.Max(j, blockFirstRow); i < blockLastRow; i++)
                    {
                        front[offsetJ + (i - j)] -= PanelDot(
                            buffer, blockBase + (i - blockFirstRow), panelWidth, tile, 0);
                    }
                }
            }
        }

        /// <summary>
        /// Собирает значения панели U[panelStart + k, j + t] для группы
        /// столбцов в непрерывный буфер, чтобы горячий цикл читал их без
        /// косвенной адресации.
        /// </summary>
        private static void GatherPanelTile(
            double[] front, int[] offsets, int panelStart, int panelWidth, int firstColumn, int columns, double[] tile)
        {
            for (int k = 0; k < panelWidth; k++)
            {
                int column = panelStart + k;
                int rowBase = offsets[column] - column;
                int destination = k * ColumnTile;

                for (int t = 0; t < columns; t++)
                    tile[destination + t] = front[rowBase + firstColumn + t];
            }
        }

        /// <summary>
        /// Скалярное произведение строки блока панели со столбцом t группы —
        /// используется только на треугольном «уголке» и на одиночных
        /// столбцах, то есть на пренебрежимой доле работы.
        /// </summary>
        private static double PanelDot(double[] buffer, int rowIndex, int panelWidth, double[] tile, int t)
        {
            double sum = 0.0;
            for (int k = 0; k < panelWidth; k++)
                sum += buffer[rowIndex + k * RowBlock] * tile[k * ColumnTile + t];

            return sum;
        }

        /// <summary>
        /// Горячее ядро: вычитает из четырёх столбцов фронта вклад всей панели
        /// сразу, обрабатывая строки векторными группами.
        /// </summary>
        private static unsafe void UpdateColumnTile(
            double[] front, int[] offsets, double[] buffer, int blockBase, int blockFirstRow,
            int rowFrom, int rowTo, int panelWidth, int firstColumn, double[] tile)
        {
            int lanes = Vector<double>.Count;
            int offset0 = offsets[firstColumn] - firstColumn;
            int offset1 = offsets[firstColumn + 1] - firstColumn - 1;
            int offset2 = offsets[firstColumn + 2] - firstColumn - 2;
            int offset3 = offsets[firstColumn + 3] - firstColumn - 3;

            // Индексация через указатели. В этом цикле проводится основная
            // часть времени факторизации, и проверки границ массивов здесь
            // обходятся примерно вдвое дороже самой арифметики. Все смещения
            // получены из символьной фазы и по построению лежат внутри
            // фронтальной матрицы и буфера панели.
            fixed (double* frontBase = front, bufferBase = buffer, tileBase = tile)
            {
                int i = rowFrom;

                for (; i + lanes <= rowTo; i += lanes)
                {
                    double* panelRow = bufferBase + blockBase + (i - blockFirstRow);

                    var accumulator0 = Vector<double>.Zero;
                    var accumulator1 = Vector<double>.Zero;
                    var accumulator2 = Vector<double>.Zero;
                    var accumulator3 = Vector<double>.Zero;

                    for (int k = 0; k < panelWidth; k++)
                    {
                        var panel = Unsafe.Read<Vector<double>>(panelRow + k * RowBlock);
                        double* scalars = tileBase + k * ColumnTile;

                        accumulator0 += panel * scalars[0];
                        accumulator1 += panel * scalars[1];
                        accumulator2 += panel * scalars[2];
                        accumulator3 += panel * scalars[3];
                    }

                    DenseKernels.SubtractVector(frontBase + offset0 + i, accumulator0);
                    DenseKernels.SubtractVector(frontBase + offset1 + i, accumulator1);
                    DenseKernels.SubtractVector(frontBase + offset2 + i, accumulator2);
                    DenseKernels.SubtractVector(frontBase + offset3 + i, accumulator3);
                }

                for (; i < rowTo; i++)
                {
                    double* panelRow = bufferBase + blockBase + (i - blockFirstRow);
                    double sum0 = 0.0, sum1 = 0.0, sum2 = 0.0, sum3 = 0.0;

                    for (int k = 0; k < panelWidth; k++)
                    {
                        double panel = panelRow[k * RowBlock];
                        double* scalars = tileBase + k * ColumnTile;

                        sum0 += panel * scalars[0];
                        sum1 += panel * scalars[1];
                        sum2 += panel * scalars[2];
                        sum3 += panel * scalars[3];
                    }

                    frontBase[offset0 + i] -= sum0;
                    frontBase[offset1 + i] -= sum1;
                    frontBase[offset2 + i] -= sum2;
                    frontBase[offset3 + i] -= sum3;
                }
            }
        }

        /// <summary>
        /// Рабочие буферы одного потока. Выделяются один раз на поток:
        /// суперузлов десятки тысяч, и выделение буферов на каждый из них
        /// создало бы заметную нагрузку на сборщик мусора.
        /// </summary>
        private sealed class WorkerContext
        {
            private readonly int retentionCapacity;
            private double[] front = Array.Empty<double>();

            public WorkerContext(int size, int maxFrontSize, int panelWidth)
            {
                long maxFrontLength = (long)maxFrontSize * (maxFrontSize + 1) / 2;
                retentionCapacity = (int)Math.Min(maxFrontLength, FrontRetentionLimit);

                RowMap = new int[size];
                ColumnOffsets = new int[maxFrontSize + 1];
                RelativeIndex = new int[Math.Max(1, maxFrontSize)];

                // Буфер панели укладывается блоками строк по RowBlock с
                // фиксированным шагом, поэтому последний блок может быть
                // неполным — место под него резервируется целиком.
                int blocks = (maxFrontSize + RowBlock - 1) / RowBlock + 1;
                PanelBuffer = new double[Math.Max(1, blocks * RowBlock * panelWidth)];
            }

            /// <summary>Глобальный номер строки -> её индекс внутри текущего фронта.</summary>
            public int[] RowMap { get; }

            /// <summary>Смещения столбцов в упакованном нижнем треугольнике фронта.</summary>
            public int[] ColumnOffsets { get; }

            /// <summary>Отображение строк вклада потомка в индексы родителя.</summary>
            public int[] RelativeIndex { get; }

            /// <summary>Буфер панели, умноженной на диагональ.</summary>
            public double[] PanelBuffer { get; }

            /// <summary>
            /// Буфер фронтальной матрицы. Поток обрабатывает не более одного
            /// фронта одновременно, поэтому одного буфера на поток достаточно,
            /// и он переиспользуется десятками тысяч раз — это снимает с
            /// сборщика мусора основную нагрузку факторизации.
            ///
            /// Буфер выделяется сразу на максимальный нужный размер, а не
            /// растёт постепенно: постепенный рост оставлял бы за собой цепочку
            /// крупных брошенных массивов в куче больших объектов, которая не
            /// уплотняется, и пиковая память оказалась бы кратно выше полезной.
            ///
            /// Фронты крупнее <see cref="FrontRetentionLimit"/> выделяются
            /// отдельно и не удерживаются. Таких фронтов единицы — у самого
            /// корня дерева; их площадь растёт как квадрат, а объём работы по
            /// ним как куб, поэтому отдельное выделение здесь пренебрежимо,
            /// зато удержание такого буфера каждым потоком добавило бы к
            /// пиковой памяти гигабайты.
            /// </summary>
            public double[] EnsureFront(int length)
            {
                if (length > FrontRetentionLimit)
                    return GC.AllocateUninitializedArray<double>(length);

                if (front.Length < length)
                    front = GC.AllocateUninitializedArray<double>(retentionCapacity);

                return front;
            }

            public int RegularizedCount;
            public int NegativeCount;
            public double SmallestPivot = double.MaxValue;
            public double LargestPivot;

            public void Observe(double pivot)
            {
                double magnitude = Math.Abs(pivot);
                if (magnitude < SmallestPivot)
                    SmallestPivot = magnitude;
                if (magnitude > LargestPivot)
                    LargestPivot = magnitude;
            }
        }

        /// <summary>
        /// Планировщик обхода суперузлового дерева. Очередь готовых суперузлов
        /// плюс счётчики необработанных потомков — этого достаточно, чтобы
        /// потоки сами находили работу без общего барьера между уровнями
        /// дерева: барьер простаивал бы на неровных деревьях, а они как раз
        /// типичны для реальных расчётных схем.
        /// </summary>
        private sealed class Schedule
        {
            private readonly ConcurrentStack<int> ready;
            private readonly int[] remainingChildren;
            private readonly int total;
            private readonly int workerCount;
            private readonly SemaphoreSlim available;

            private int completed;
            private int finished;
            private int activeWorkers;
            private Exception? failure;

            public Schedule(ConcurrentStack<int> ready, int[] remainingChildren, int total, int initialReady, int workerCount)
            {
                this.ready = ready;
                this.remainingChildren = remainingChildren;
                this.total = total;
                this.workerCount = workerCount;
                available = new SemaphoreSlim(initialReady);
            }

            public int ActiveWorkers => Volatile.Read(ref activeWorkers);

            public void EnterWork() => Interlocked.Increment(ref activeWorkers);

            public void LeaveWork() => Interlocked.Decrement(ref activeWorkers);

            public bool TryTake(out int supernode)
            {
                while (true)
                {
                    available.Wait();

                    if (Volatile.Read(ref finished) != 0)
                    {
                        supernode = -1;
                        return false;
                    }

                    if (ready.TryPop(out supernode))
                        return true;

                    // Защита от гонки, которой быть не должно: разрешение
                    // возвращается, чтобы никто не заснул навсегда.
                    available.Release();
                    Thread.Yield();
                }
            }

            public void Complete(int supernode, int parent)
            {
                if (parent >= 0 && Interlocked.Decrement(ref remainingChildren[parent]) == 0)
                {
                    ready.Push(parent);
                    available.Release();
                }

                if (Interlocked.Increment(ref completed) == total)
                    Stop();
            }

            public void Fail(Exception exception)
            {
                Interlocked.CompareExchange(ref failure, exception, (Exception?)null);
                Stop();
            }

            private void Stop()
            {
                Volatile.Write(ref finished, 1);
                available.Release(workerCount);
            }

            public void ThrowIfFailed()
            {
                var captured = Volatile.Read(ref failure);
                if (captured != null)
                    throw new InvalidOperationException("Численная факторизация не завершена.", captured);
            }
        }
    }
}
