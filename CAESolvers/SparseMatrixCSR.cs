namespace CAESolvers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Разреженная матрица в формате CSR для сборки и хранения матриц МКЭ.
    /// Работает в две фазы:
    ///  1) Сборка — <see cref="AddToElement"/> накапливает (суммирует) вклады
    ///     локальных матриц элементов в глобальные позиции (row, col).
    ///  2) После <see cref="FinalizeAssembly"/> структура разреженности
    ///     фиксируется: доступ по индексам ищется бинарным поиском по
    ///     отсортированным столбцам строки (O(log k), k — число ненулевых
    ///     в строке); добавить новую ненулевую позицию уже нельзя — только обновить существующую.
    ///     Это исключает точечные вставки в CSR-массивы "на живую", которые
    ///     иначе рассинхронизируют rowPointers с реальным положением данных.
    /// </summary>
    public class SparseMatrixCSR
    {
        private double[] values;      // Значения ненулевых элементов
        private int[] colIndices;     // Индексы столбцов
        private int[] rowPointers;    // Указатели на начало строк

        // Буфер накопления на время сборки (до FinalizeAssembly)
        private Dictionary<(int row, int col), double> assemblyBuffer;

        private readonly int rows;
        private readonly int cols;
        private int nonZeroCount;
        private bool isFinalized;

        private const double Tolerance = 1e-15;

        public SparseMatrixCSR(int rows, int cols)
        {
            this.rows = rows;
            this.cols = cols;
            assemblyBuffer = new Dictionary<(int, int), double>();
            isFinalized = false;
        }

        /// <summary>
        /// Построение сразу из списка вкладов (координатный формат).
        /// Повторяющиеся (row, col) суммируются, как при сборке МКЭ.
        /// </summary>
        public SparseMatrixCSR(int rows, int cols, IEnumerable<(int row, int col, double value)> elements)
            : this(rows, cols)
        {
            foreach (var e in elements)
                AddToElement(e.row, e.col, e.value);

            FinalizeAssembly();
        }

        /// <summary>
        /// Добавляет вклад к элементу матрицы (аналог K[i,j] += local[i,j] при сборке МКЭ).
        /// До финализации накапливается в буфере; после — обновляет только
        /// уже существующую ненулевую позицию (новую позицию завести нельзя).
        /// </summary>
        public void AddToElement(int row, int col, double value)
        {
            CheckBounds(row, col);

            if (!isFinalized)
            {
                assemblyBuffer.TryGetValue((row, col), out double existing);
                assemblyBuffer[(row, col)] = existing + value;
                return;
            }

            int index = FindIndex(row, col);
            if (index < 0)
                throw new InvalidOperationException(
                    $"Позиция ({row}, {col}) отсутствует в структуре разреженности. " +
                    "После FinalizeAssembly() можно изменять только уже существующие " +
                    "ненулевые элементы — включите эту позицию в сборку заранее.");

            values[index] += value;
        }

        /// <summary>
        /// Завершает сборку: строит компактные CSR-массивы из накопленного буфера
        /// и фиксирует структуру разреженности.
        /// </summary>
        public void FinalizeAssembly()
        {
            if (isFinalized)
                throw new InvalidOperationException("Матрица уже финализирована.");

            var elements = assemblyBuffer
                .Where(kv => Math.Abs(kv.Value) > Tolerance)
                .Select(kv => (kv.Key.row, kv.Key.col, kv.Value))
                .ToList();

            BuildFromCoordinateFormat(elements);

            assemblyBuffer = null;
            isFinalized = true;
        }

        private void BuildFromCoordinateFormat(List<(int row, int col, double value)> elements)
        {
            var byRow = elements
                .GroupBy(e => e.row)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.col).ToList());

            nonZeroCount = elements.Count;
            values = new double[nonZeroCount];
            colIndices = new int[nonZeroCount];
            rowPointers = new int[rows + 1];

            int currentIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                rowPointers[row] = currentIndex;

                if (byRow.TryGetValue(row, out var items))
                {
                    foreach (var item in items)
                    {
                        values[currentIndex] = item.value;
                        colIndices[currentIndex] = item.col;
                        currentIndex++;
                    }
                }
            }

            rowPointers[rows] = currentIndex;
        }

        /// <summary>
        /// Прямой доступ к элементу матрицы по глобальным индексам.
        /// Во время сборки читает/пишет буфер; после финализации — CSR-массивы
        /// через бинарный поиск по отсортированным столбцам строки
        /// (запись возможна только в уже существующую ненулевую позицию).
        /// </summary>
        public double this[int row, int col]
        {
            get
            {
                CheckBounds(row, col);

                if (!isFinalized)
                    return assemblyBuffer.TryGetValue((row, col), out double v) ? v : 0.0;

                int index = FindIndex(row, col);
                return index >= 0 ? values[index] : 0.0;
            }
            set
            {
                CheckBounds(row, col);

                if (!isFinalized)
                {
                    assemblyBuffer[(row, col)] = value;
                    return;
                }

                int index = FindIndex(row, col);
                if (index < 0)
                    throw new InvalidOperationException(
                        $"Позиция ({row}, {col}) отсутствует в структуре разреженности после финализации.");

                values[index] = value;
            }
        }

        private void CheckBounds(int row, int col)
        {
            if (row < 0 || row >= rows || col < 0 || col >= cols)
                throw new IndexOutOfRangeException($"Индексы вне диапазона: ({row}, {col})");
        }

        /// <summary>
        /// Бинарный поиск позиции (row, col) в отсортированном по столбцам
        /// сегменте строки. Возвращает -1, если элемент структурно нулевой.
        /// </summary>
        private int FindIndex(int row, int col)
        {
            int lo = rowPointers[row];
            int hi = rowPointers[row + 1] - 1;

            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                int c = colIndices[mid];

                if (c == col) return mid;
                if (c < col) lo = mid + 1; else hi = mid - 1;
            }

            return -1;
        }

        /// <summary>
        /// Умножение матрицы на вектор.
        /// </summary>
        public double[] Multiply(double[] vector)
        {
            RequireFinalized();

            if (vector.Length != cols)
                throw new ArgumentException($"Размер вектора {vector.Length} не соответствует числу столбцов {cols}");

            var result = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double sum = 0.0;
                int start = rowPointers[i];
                int end = rowPointers[i + 1];

                for (int j = start; j < end; j++)
                    sum += values[j] * vector[colIndices[j]];

                result[i] = sum;
            }

            return result;
        }

        /// <summary>
        /// Получение строки матрицы в виде словаря (индекс столбца -> значение).
        /// </summary>
        public Dictionary<int, double> GetRow(int row)
        {
            RequireFinalized();

            if (row < 0 || row >= rows)
                throw new IndexOutOfRangeException($"Индекс строки {row} вне диапазона");

            var rowElements = new Dictionary<int, double>();
            int start = rowPointers[row];
            int end = rowPointers[row + 1];

            for (int i = start; i < end; i++)
                rowElements[colIndices[i]] = values[i];

            return rowElements;
        }

        public int NonZeroCount => isFinalized
            ? nonZeroCount
            : assemblyBuffer.Count(kv => Math.Abs(kv.Value) > Tolerance);

        public (int rows, int cols) Size => (rows, cols);

        public bool IsFinalized => isFinalized;

        public bool IsZero(int row, int col)
        {
            CheckBounds(row, col);
            return !isFinalized
                ? !assemblyBuffer.ContainsKey((row, col)) || Math.Abs(assemblyBuffer[(row, col)]) <= Tolerance
                : FindIndex(row, col) < 0;
        }

        private void RequireFinalized()
        {
            if (!isFinalized)
                throw new InvalidOperationException("Матрица не финализирована — вызовите FinalizeAssembly() после сборки.");
        }

        /// <summary>
        /// Вывод матрицы в консоль (для отладки, только для небольших матриц).
        /// </summary>
        public void Print()
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double value = this[i, j];
                    Console.Write($"{value,8:F3} ");
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Вывод внутреннего представления CSR.
        /// </summary>
        public void PrintCSR()
        {
            RequireFinalized();

            Console.WriteLine("CSR Representation:");
            Console.WriteLine($"Rows: {rows}, Cols: {cols}, NonZero: {nonZeroCount}");
            Console.WriteLine($"RowPointers: [{string.Join(", ", rowPointers)}]");
            Console.WriteLine($"ColIndices:  [{string.Join(", ", colIndices)}]");
            Console.WriteLine($"Values:      [{string.Join(", ", values.Select(v => v.ToString("F3")))}]");
        }
    }
}
