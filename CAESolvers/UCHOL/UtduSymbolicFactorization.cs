namespace CAESolvers
{
    using System;

    /// <summary>
    /// Алгоритм переупорядочивания, снижающего заполнение множителя.
    /// </summary>
    public enum FillReducingOrdering
    {
        /// <summary>
        /// Без снижения заполнения: порядок уравнений сохраняется (применяется
        /// только обратный обход дерева исключений, необходимый для
        /// суперузлового разбиения). Предназначено для отладки и для матриц,
        /// уже упорядоченных внешним средством, — на трёхмерной задаче в
        /// сотни тысяч уравнений приведёт к неприемлемому расходу памяти.
        /// </summary>
        Natural,

        /// <summary>
        /// Приближённая минимальная степень (AMD) — выбор по умолчанию.
        /// См. <see cref="ApproximateMinimumDegreeOrdering"/>.
        /// </summary>
        ApproximateMinimumDegree
    }

    /// <summary>
    /// Настройки прямого решателя <see cref="SymmetricUtduSolver"/>.
    /// Значения по умолчанию рассчитаны на типичную МКЭ-матрицу жёсткости в
    /// сотни тысяч уравнений и обычно не требуют подстройки.
    /// </summary>
    public sealed class UtduSolverOptions
    {
        /// <summary>Алгоритм переупорядочивания.</summary>
        public FillReducingOrdering Ordering { get; set; } = FillReducingOrdering.ApproximateMinimumDegree;

        /// <summary>
        /// Максимальное число потоков численной факторизации. 0 (по умолчанию)
        /// означает «по числу логических ядер». Значение 1 даёт полностью
        /// последовательный проход — удобно для сравнения и отладки.
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; }

        /// <summary>
        /// Верхняя граница числа столбцов в суперузле при объединении.
        /// Слишком крупные суперузлы начинают хранить лишние нули и ухудшают
        /// параллелизм по дереву, слишком мелкие — не дают выигрыша от плотных
        /// блочных операций.
        /// </summary>
        public int SupernodeAmalgamationLimit { get; set; } = 64;

        /// <summary>
        /// Ширина панели блочной факторизации фронта. Определяет размер
        /// рабочего блока, который должен помещаться в кеш второго уровня.
        /// </summary>
        public int PanelWidth { get; set; } = 32;

        /// <summary>
        /// Разрешить регуляризацию диагонали. Матрица ожидается симметричной
        /// положительно определённой, но у почти вырожденных задач
        /// (недостаточно закреплённая расчётная схема, вырожденные элементы)
        /// очередной ведущий элемент может оказаться неположительным. Если
        /// регуляризация разрешена, такой пивот заменяется малым положительным
        /// значением, факторизация доводится до конца, а число таких замен
        /// возвращается в результате — решение при этом становится решением
        /// возмущённой задачи, и об этом обязательно нужно сообщать
        /// пользователю расчёта. Если запрещена — бросается исключение.
        /// </summary>
        public bool AllowDiagonalRegularization { get; set; } = true;

        /// <summary>
        /// Относительный порог, ниже которого ведущий элемент считается
        /// неприемлемым: пивот d признаётся нормальным при
        /// d &gt; PivotTolerance * max|A[i,i]|.
        /// </summary>
        public double PivotTolerance { get; set; } = 1e-13;

        /// <summary>
        /// Относительная величина, которой заменяется забракованный ведущий
        /// элемент: d = DiagonalRegularization * max|A[i,i]|.
        /// </summary>
        public double DiagonalRegularization { get; set; } = 1e-10;

        /// <summary>
        /// Минимальное число операций в блочном обновлении фронта, начиная с
        /// которого имеет смысл распараллеливать обновление внутри одного
        /// суперузла. Ближе к корню дерева независимых суперузлов почти не
        /// остаётся, и без внутреннего параллелизма ядра простаивали бы.
        /// </summary>
        public long ParallelUpdateThreshold { get; set; } = 1L << 20;

        internal int ResolveWorkerCount()
        {
            int requested = MaxDegreeOfParallelism > 0 ? MaxDegreeOfParallelism : Environment.ProcessorCount;
            return Math.Max(1, requested);
        }

        internal int ResolvePanelWidth() => Math.Clamp(PanelWidth, 4, 128);
    }

    /// <summary>
    /// Результат символьной фазы: перестановка, структура множителя и
    /// суперузловое разбиение. Не содержит ни одного числа из матрицы, поэтому
    /// может быть вычислен один раз и переиспользован для любого числа
    /// повторных факторизаций, пока структура разреженности не изменилась.
    /// Именно это делает решатель пригодным для нелинейных расчётов, где
    /// матрица жёсткости пересобирается на каждой итерации, а её портрет
    /// остаётся тем же.
    /// </summary>
    public sealed class UtduSymbolicFactorization
    {
        internal UtduSymbolicFactorization(
            int[] permutation, int[] inversePermutation,
            PermutedSymmetricPattern pattern, SupernodalStructure supernodes,
            int[] eliminationTreeParent, int[] columnCounts, int matrixNonZeroCount)
        {
            Permutation = permutation;
            InversePermutation = inversePermutation;
            Pattern = pattern;
            Supernodes = supernodes;
            EliminationTreeParent = eliminationTreeParent;
            ColumnCounts = columnCounts;
            MatrixNonZeroCount = matrixNonZeroCount;
            Layout = FactorStorageLayout.Build(supernodes);
        }

        /// <summary>Число уравнений.</summary>
        public int Size => Permutation.Length;

        /// <summary>
        /// Permutation[k] — исходный номер уравнения, стоящего на k-м месте в
        /// порядке исключения.
        /// </summary>
        public int[] Permutation { get; }

        /// <summary>InversePermutation[original] — место уравнения в порядке исключения.</summary>
        public int[] InversePermutation { get; }

        internal PermutedSymmetricPattern Pattern { get; }

        internal SupernodalStructure Supernodes { get; }

        internal int[] EliminationTreeParent { get; }

        internal int[] ColumnCounts { get; }

        internal FactorStorageLayout Layout { get; }

        internal int MatrixNonZeroCount { get; }

        /// <summary>Число суперузлов.</summary>
        public int SupernodeCount => Supernodes.Count;

        /// <summary>
        /// Число хранимых элементов множителя, включая явные нули блочного
        /// суперузлового хранения.
        /// </summary>
        public long FactorEntryCount => Supernodes.FactorEntryCount;

        /// <summary>
        /// Число ненулевых элементов множителя без учёта явных нулей блочного
        /// хранения — «честное» заполнение, по которому оценивают качество
        /// переупорядочивания.
        /// </summary>
        public long StrictFactorNonZeroCount { get; internal set; }

        /// <summary>Приближённое число арифметических операций численной факторизации.</summary>
        public long FactorOperationCount => Supernodes.FactorOperationCount;

        /// <summary>Максимальный размер фронтальной матрицы (число строк и столбцов).</summary>
        public int MaxFrontSize => Supernodes.MaxFrontSize;

        /// <summary>
        /// Память под сам множитель, байт. Это основная и неустранимая часть
        /// расхода памяти прямого решателя.
        /// </summary>
        public long EstimatedFactorBytes => FactorEntryCount * sizeof(double);

        /// <summary>
        /// Память под наибольшую фронтальную матрицу, байт. На трёхмерных
        /// задачах это может быть сотни мегабайт: фронт у корня дерева хранится
        /// плотно.
        /// </summary>
        public long MaxFrontBytes => (long)MaxFrontSize * (MaxFrontSize + 1) / 2 * sizeof(double);

        /// <summary>
        /// Оценка пиковой памяти численной факторизации, байт: множитель,
        /// фронтальные матрицы обрабатываемых одновременно суперузлов и
        /// матрицы вкладов, ожидающие сборки в родителя.
        ///
        /// Эту величину имеет смысл проверить до запуска численной фазы: на
        /// задаче в сотни тысяч уравнений именно память, а не время, обычно и
        /// определяет, применим ли прямой решатель. Символьная фаза дешёвая, и
        /// узнать оценку заранее гораздо лучше, чем упасть по нехватке памяти
        /// через несколько минут счёта.
        ///
        /// Объём одновременно живущих матриц вкладов посчитан точно для
        /// последовательного обхода; при работе в несколько потоков он несколько
        /// выше, поскольку одновременно продвигается несколько путей к корню.
        /// Не учитываются накладные расходы сборщика мусора, поэтому
        /// фактический рабочий набор процесса обычно на десятки процентов
        /// больше этой оценки.
        /// </summary>
        public long EstimatePeakBytes(int workerCount)
        {
            long workers = Math.Max(1, workerCount);

            // Каждый поток удерживает один буфер фронта, но не больше
            // установленной границы; фронты крупнее неё выделяются отдельно и
            // живут только на время своей факторизации.
            long retained = Math.Min(
                MaxFrontBytes,
                (long)SupernodalMultifrontalFactorizer.FrontRetentionLimit * sizeof(double));

            return EstimatedFactorBytes
                 + StructureBytes
                 + PeakContributionBytes
                 + workers * retained
                 + MaxFrontBytes;
        }

        /// <summary>
        /// Память под символьные структуры (структура разреженности
        /// переставленной матрицы и структуры строк суперузлов), байт.
        /// </summary>
        /// <summary>
        /// Пиковый суммарный объём матриц вкладов, ожидающих сборки в
        /// родительский суперузел, байт — вторая по величине статья расхода
        /// памяти после самого множителя.
        /// </summary>
        public long PeakContributionBytes => Supernodes.PeakContributionEntries * sizeof(double);

        public long StructureBytes =>
            (long)(Pattern.Rows.Length + Pattern.LowerValueIndices.Length
                 + Supernodes.PatternRows.Length) * sizeof(int);

        /// <summary>
        /// Выполняет символьный анализ: переупорядочивание, дерево исключений,
        /// длины столбцов множителя и суперузловое разбиение.
        /// </summary>
        public static UtduSymbolicFactorization Analyze(SymmetricCSRMatrix matrix, UtduSolverOptions options)
        {
            if (matrix == null)
                throw new ArgumentNullException(nameof(matrix));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            int n = matrix.Size;

            // 1. Переупорядочивание для снижения заполнения.
            int[] fillReducing;
            if (options.Ordering == FillReducingOrdering.ApproximateMinimumDegree)
            {
                var graph = SymmetricPatternGraph.FromMatrix(matrix);
                fillReducing = ApproximateMinimumDegreeOrdering.Compute(graph);
            }
            else
            {
                fillReducing = new int[n];
                for (int i = 0; i < n; i++)
                    fillReducing[i] = i;
            }

            // 2. Обратный обход дерева исключений. После него столбцы одного
            // суперузла идут подряд, а поддеревья занимают непрерывные
            // диапазоны — без этого суперузлы получились бы разорванными, а
            // мультифронтальная схема потеряла бы локальность.
            var firstInverse = Invert(fillReducing);
            var firstPattern = PermutedSymmetricPattern.Create(matrix, firstInverse);
            var firstParent = EliminationTree.Build(n, firstPattern.Pointers, firstPattern.Rows);
            var postorder = EliminationTree.Postorder(n, firstParent);

            var permutation = new int[n];
            for (int k = 0; k < n; k++)
                permutation[k] = fillReducing[postorder[k]];

            // 3. Окончательная структура и символьные величины.
            var inversePermutation = Invert(permutation);
            var pattern = PermutedSymmetricPattern.Create(matrix, inversePermutation);
            var parent = EliminationTree.Build(n, pattern.Pointers, pattern.Rows);
            var finalPostorder = EliminationTree.Postorder(n, parent);
            var columnCounts = EliminationTree.ColumnCounts(n, pattern.Pointers, pattern.Rows, parent, finalPostorder);

            var supernodes = SupernodalStructure.Build(
                pattern, parent, columnCounts, Math.Max(1, options.SupernodeAmalgamationLimit));

            long strictNonZeros = 0;
            for (int j = 0; j < n; j++)
                strictNonZeros += columnCounts[j];

            return new UtduSymbolicFactorization(
                permutation, inversePermutation, pattern, supernodes, parent, columnCounts, matrix.NonZeroCount)
            {
                StrictFactorNonZeroCount = strictNonZeros
            };
        }

        private static int[] Invert(int[] permutation)
        {
            var inverse = new int[permutation.Length];
            for (int k = 0; k < permutation.Length; k++)
                inverse[permutation[k]] = k;

            return inverse;
        }

        internal void EnsureCompatible(SymmetricCSRMatrix matrix)
        {
            if (matrix.Size != Size)
                throw new ArgumentException(
                    $"Размер матрицы {matrix.Size} не соответствует символьной факторизации, " +
                    $"построенной для {Size} уравнений.");

            if (matrix.NonZeroCount != MatrixNonZeroCount)
                throw new ArgumentException(
                    "Структура разреженности матрицы изменилась с момента символьного анализа " +
                    $"(было {MatrixNonZeroCount} ненулевых элементов, стало {matrix.NonZeroCount}). " +
                    "Переиспользовать символьную факторизацию можно только при неизменной структуре — " +
                    "выполните Analyze заново.");
        }
    }
}
