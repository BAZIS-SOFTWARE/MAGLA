namespace CAESolvers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Разбиение множителя на суперузлы и суперузловое дерево сборки.
    ///
    /// Суперузел — это группа идущих подряд столбцов множителя с (почти)
    /// одинаковой структурой разреженности. Такую группу можно хранить и
    /// обрабатывать как плотный блок, и это принципиально меняет
    /// производительность: вместо миллионов операций над отдельными числами с
    /// косвенной адресацией получаются плотные блочные операции, которые
    /// эффективно используют кеш и векторные инструкции. Для типичных
    /// МКЭ-матриц выигрыш составляет от трёх до десяти раз.
    ///
    /// Второе, что даёт суперузловое дерево, — распараллеливание: суперузел
    /// можно факторизовать, как только готовы все его потомки, поэтому
    /// независимые поддеревья считаются одновременно на разных ядрах, а
    /// синхронизация нужна только между потомком и родителем.
    ///
    /// Границы суперузлов выбираются так: сначала находятся «фундаментальные»
    /// суперузлы, внутри которых структура столбцов вкладывается точно и
    /// хранение плотным блоком не добавляет ни одного нуля; затем соседние
    /// суперузлы объединяются, если добавленных нулей мало по сравнению с
    /// выигрышем от более крупных плотных блоков (relaxed amalgamation).
    /// </summary>
    public sealed class SupernodalStructure
    {
        private SupernodalStructure(
            int size, int[] firstColumn, int[] supernodeOf, int[] parent,
            int[] childPointers, int[] children, int[] patternPointers, int[] patternRows)
        {
            Size = size;
            FirstColumn = firstColumn;
            SupernodeOf = supernodeOf;
            Parent = parent;
            ChildPointers = childPointers;
            Children = children;
            PatternPointers = patternPointers;
            PatternRows = patternRows;
        }

        /// <summary>Число уравнений.</summary>
        public int Size { get; }

        /// <summary>Число суперузлов.</summary>
        public int Count => FirstColumn.Length - 1;

        /// <summary>
        /// Границы суперузлов по столбцам, длина Count + 1: суперузлу s
        /// принадлежат столбцы FirstColumn[s] .. FirstColumn[s+1]-1.
        /// </summary>
        public int[] FirstColumn { get; }

        /// <summary>SupernodeOf[j] — номер суперузла, содержащего столбец j.</summary>
        public int[] SupernodeOf { get; }

        /// <summary>Родитель суперузла в дереве сборки, либо -1 для корня.</summary>
        public int[] Parent { get; }

        /// <summary>Указатели списков потомков, длина Count + 1.</summary>
        public int[] ChildPointers { get; }

        /// <summary>Потомки суперузлов, сгруппированные по родителю.</summary>
        public int[] Children { get; }

        /// <summary>Указатели структур строк суперузлов, длина Count + 1.</summary>
        public int[] PatternPointers { get; }

        /// <summary>
        /// Структура строк суперузлов: для суперузла s это отсортированный по
        /// возрастанию список глобальных номеров строк, в которых его столбцы
        /// имеют ненулевые элементы. Первые (FirstColumn[s+1]-FirstColumn[s])
        /// позиций — это сами столбцы суперузла (диагональный блок), остальное
        /// — «подвал», через который идёт вклад в родителя.
        /// </summary>
        public int[] PatternRows { get; }

        /// <summary>Число хранимых элементов множителя (включая явные нули блочного хранения).</summary>
        public long FactorEntryCount { get; private set; }

        /// <summary>Приближённое число операций численной факторизации.</summary>
        public long FactorOperationCount { get; private set; }

        /// <summary>Максимальный размер фронтальной матрицы (число строк).</summary>
        public int MaxFrontSize { get; private set; }

        /// <summary>Максимальное число столбцов в суперузле.</summary>
        public int MaxSupernodeWidth { get; private set; }

        /// <summary>Число строк фронта суперузла s.</summary>
        public int FrontSize(int supernode) => PatternPointers[supernode + 1] - PatternPointers[supernode];

        /// <summary>Число столбцов (пивотов) суперузла s.</summary>
        public int Width(int supernode) => FirstColumn[supernode + 1] - FirstColumn[supernode];

        /// <summary>
        /// Строит суперузловое разбиение по дереву исключений и длинам
        /// столбцов множителя.
        /// </summary>
        public static SupernodalStructure Build(
            PermutedSymmetricPattern pattern, int[] parent, int[] columnCounts, int amalgamationLimit)
        {
            int n = pattern.Size;

            var childCount = new int[n];
            for (int j = 0; j < n; j++)
            {
                if (parent[j] >= 0)
                    childCount[parent[j]]++;
            }

            var fundamentalStarts = FindFundamentalSupernodes(n, parent, columnCounts, childCount);
            var boundaries = Amalgamate(n, fundamentalStarts, parent, columnCounts, amalgamationLimit, out int[] frontSizes);

            int supernodeCount = boundaries.Length - 1;

            var supernodeOf = new int[n];
            for (int s = 0; s < supernodeCount; s++)
            {
                for (int j = boundaries[s]; j < boundaries[s + 1]; j++)
                    supernodeOf[j] = s;
            }

            // Суперузловое дерево: родитель — суперузел, содержащий родителя
            // последнего столбца.
            var superParent = new int[supernodeCount];
            var superChildCount = new int[supernodeCount];
            for (int s = 0; s < supernodeCount; s++)
            {
                int lastColumn = boundaries[s + 1] - 1;
                int parentColumn = parent[lastColumn];
                superParent[s] = parentColumn < 0 ? -1 : supernodeOf[parentColumn];

                if (superParent[s] >= 0)
                    superChildCount[superParent[s]]++;
            }

            var childPointers = new int[supernodeCount + 1];
            for (int s = 0; s < supernodeCount; s++)
                childPointers[s + 1] = childPointers[s] + superChildCount[s];

            var children = new int[supernodeCount == 0 ? 0 : childPointers[supernodeCount]];
            var childCursor = new int[supernodeCount];
            Array.Copy(childPointers, childCursor, supernodeCount);
            for (int s = 0; s < supernodeCount; s++)
            {
                if (superParent[s] >= 0)
                    children[childCursor[superParent[s]]++] = s;
            }

            var patternPointers = new int[supernodeCount + 1];
            for (int s = 0; s < supernodeCount; s++)
                patternPointers[s + 1] = patternPointers[s] + frontSizes[s];

            var patternRows = new int[supernodeCount == 0 ? 0 : patternPointers[supernodeCount]];

            var structure = new SupernodalStructure(
                n, boundaries, supernodeOf, superParent, childPointers, children, patternPointers, patternRows);

            structure.BuildPatterns(pattern);
            structure.ComputeCosts();

            return structure;
        }

        /// <summary>
        /// Находит начала фундаментальных суперузлов. Столбец j продолжает
        /// суперузел столбца j-1 только если он его единственный родитель в
        /// дереве и структура столбца j-1 без диагонали в точности совпадает
        /// со структурой столбца j. Тогда плотное блочное хранение не добавляет
        /// ни одного явного нуля.
        /// </summary>
        private static int[] FindFundamentalSupernodes(int n, int[] parent, int[] columnCounts, int[] childCount)
        {
            var starts = new List<int>();

            for (int j = 0; j < n; j++)
            {
                bool startsNew =
                    j == 0 ||
                    parent[j - 1] != j ||
                    childCount[j] > 1 ||
                    columnCounts[j - 1] != columnCounts[j] + 1;

                if (startsNew)
                    starts.Add(j);
            }

            starts.Add(n);
            return starts.ToArray();
        }

        /// <summary>
        /// Объединяет соседние суперузлы, пока это выгодно. Объединять можно
        /// только суперузел с его родителем по дереву (тогда столбцы остаются
        /// цепочкой, а структура «подвала» потомка гарантированно вкладывается
        /// в структуру родителя). Критерий — доля явных нулей в получившемся
        /// плотном блоке; для узких блоков она допускается большой, потому что
        /// накладные расходы на мелкий суперузел всё равно перевешивают.
        /// </summary>
        private static int[] Amalgamate(
            int n, int[] fundamentalStarts, int[] parent, int[] columnCounts,
            int amalgamationLimit, out int[] frontSizes)
        {
            int fundamentalCount = fundamentalStarts.Length - 1;

            var columnCountPrefix = new long[n + 1];
            for (int j = 0; j < n; j++)
                columnCountPrefix[j + 1] = columnCountPrefix[j] + columnCounts[j];

            var boundaries = new List<int>();
            var fronts = new List<int>();

            int index = 0;
            while (index < fundamentalCount)
            {
                int first = fundamentalStarts[index];
                int last = fundamentalStarts[index + 1] - 1;
                int columns = last - first + 1;
                int frontSize = columnCounts[first];
                long nonZeros = columnCountPrefix[last + 1] - columnCountPrefix[first];

                while (index + 1 < fundamentalCount)
                {
                    // Слияние допустимо только вверх по дереву.
                    if (parent[last] != last + 1)
                        break;

                    int nextLast = fundamentalStarts[index + 2] - 1;
                    int nextFirst = fundamentalStarts[index + 1];
                    int mergedColumns = columns + (nextLast - nextFirst + 1);

                    if (mergedColumns > amalgamationLimit)
                        break;

                    // Структура объединённого суперузла — это собственные
                    // столбцы потомка плюс вся структура родителя.
                    long mergedFront = (long)columns + columnCounts[nextFirst];
                    long stored = mergedFront * mergedColumns - (long)mergedColumns * (mergedColumns - 1) / 2;
                    long mergedNonZeros = nonZeros + (columnCountPrefix[nextLast + 1] - columnCountPrefix[nextFirst]);

                    if (!IsMergeWorthwhile(mergedColumns, stored - mergedNonZeros, stored))
                        break;

                    last = nextLast;
                    columns = mergedColumns;
                    frontSize = (int)mergedFront;
                    nonZeros = mergedNonZeros;
                    index++;
                }

                boundaries.Add(first);
                fronts.Add(frontSize);
                index++;
            }

            boundaries.Add(n);
            frontSizes = fronts.ToArray();
            return boundaries.ToArray();
        }

        /// <summary>
        /// Градуированный критерий слияния: чем шире получающийся блок, тем
        /// меньше «мусорных» нулей мы готовы терпеть. Пороги подобраны как в
        /// зрелых суперузловых решателях и на практике дают заполнение в
        /// пределах нескольких процентов от оптимального при заметно более
        /// крупных плотных блоках.
        /// </summary>
        private static bool IsMergeWorthwhile(int columns, long addedZeros, long stored)
        {
            if (addedZeros <= 0)
                return true;

            double fraction = (double)addedZeros / stored;

            if (columns <= 4)
                return fraction <= 0.8;
            if (columns <= 16)
                return fraction <= 0.1;
            if (columns <= 48)
                return fraction <= 0.05;

            return false;
        }

        /// <summary>
        /// Вычисляет структуру строк каждого суперузла. Структура суперузла —
        /// это объединение нижних частей столбцов исходной матрицы и «подвалов»
        /// всех его потомков; это следствие того, что структура столбца
        /// множителя равна объединению структур его детей в дереве исключений.
        /// Суперузлы обходятся по возрастанию номера, что гарантирует
        /// готовность всех потомков.
        /// </summary>
        private void BuildPatterns(PermutedSymmetricPattern pattern)
        {
            int supernodeCount = Count;
            var flag = new int[Size];
            Array.Fill(flag, -1);

            for (int s = 0; s < supernodeCount; s++)
            {
                int first = FirstColumn[s];
                int last = FirstColumn[s + 1] - 1;
                int write = PatternPointers[s];

                // Диагональный блок — сами столбцы суперузла.
                for (int j = first; j <= last; j++)
                {
                    flag[j] = s;
                    PatternRows[write++] = j;
                }

                for (int j = first; j <= last; j++)
                {
                    int end = pattern.Pointers[j + 1];
                    for (int p = pattern.LowerStart[j]; p < end; p++)
                    {
                        int row = pattern.Rows[p];
                        if (flag[row] == s)
                            continue;

                        flag[row] = s;
                        PatternRows[write++] = row;
                    }
                }

                int childEnd = ChildPointers[s + 1];
                for (int c = ChildPointers[s]; c < childEnd; c++)
                {
                    int child = Children[c];
                    int childWidth = Width(child);
                    int patternEnd = PatternPointers[child + 1];

                    for (int q = PatternPointers[child] + childWidth; q < patternEnd; q++)
                    {
                        int row = PatternRows[q];
                        if (flag[row] == s)
                            continue;

                        flag[row] = s;
                        PatternRows[write++] = row;
                    }
                }

                if (write != PatternPointers[s + 1])
                    throw new InvalidOperationException(
                        $"Символьная фаза: предсказанный размер фронта суперузла {s} " +
                        $"({PatternPointers[s + 1] - PatternPointers[s]}) не совпал с фактическим " +
                        $"({write - PatternPointers[s]}). Это внутренняя ошибка символьного анализа.");

                Array.Sort(PatternRows, PatternPointers[s], FrontSize(s));
            }
        }

        /// <summary>
        /// Наибольший суммарный размер (в элементах) матриц вкладов, живущих
        /// одновременно при обратном обходе дерева.
        ///
        /// В мультифронтальной схеме матрица вкладов суперузла рождается при
        /// его факторизации и умирает при сборке родителя, поэтому в каждый
        /// момент их живёт несколько — вдоль пути к корню. Их суммарный объём
        /// сравним с размером самого множителя и является второй по величине
        /// статьёй расхода памяти; посчитать его точно можно уже в символьной
        /// фазе, что и делается здесь.
        /// </summary>
        public long PeakContributionEntries { get; private set; }

        private void ComputeCosts()
        {
            long entries = 0;
            long operations = 0;
            int maxFront = 0;
            int maxWidth = 0;

            var contribution = new long[Count];
            long live = 0;
            long peak = 0;

            for (int s = 0; s < Count; s++)
            {
                long front = FrontSize(s);
                long width = Width(s);

                entries += front * width - width * (width - 1) / 2;

                // На каждом пивоте k обновляется треугольник размера
                // (front - k), что даёт примерно (front-k)^2 операций.
                for (long k = 0; k < width; k++)
                {
                    long remaining = front - k;
                    operations += remaining * remaining;
                }

                maxFront = Math.Max(maxFront, (int)front);
                maxWidth = Math.Max(maxWidth, (int)width);

                // Пик приходится на момент, когда фронт суперузла уже создан,
                // а вклады его потомков ещё не поглощены.
                peak = Math.Max(peak, live + front * (front + 1) / 2);

                int childEnd = ChildPointers[s + 1];
                for (int c = ChildPointers[s]; c < childEnd; c++)
                    live -= contribution[Children[c]];

                long trailing = front - width;
                contribution[s] = trailing * (trailing + 1) / 2;
                live += contribution[s];
                peak = Math.Max(peak, live);
            }

            FactorEntryCount = entries;
            FactorOperationCount = operations;
            MaxFrontSize = maxFront;
            MaxSupernodeWidth = maxWidth;
            PeakContributionEntries = peak;
        }
    }
}
