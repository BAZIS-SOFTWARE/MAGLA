namespace CAESolvers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Разреженная симметричная матрица (A[i,j] == A[j,i]) в формате CSR,
    /// хранящая только верхний треугольник (row &lt;= col) вместе с диагональю.
    /// Подходит для задач упругости, теплопроводности и т.п., где матрица
    /// жёсткости симметрична, и экономит примерно вдвое память и число
    /// операций по сравнению с общим <see cref="SparseMatrixCSR"/>.
    ///
    /// ВАЖНО: индексы (row, col) в <see cref="AddToElement"/> / this[,]
    /// нормализуются внутри класса (row, col) -> (min, max), поэтому
    /// каждый физический вклад нужно добавлять РОВНО ОДИН РАЗ — не нужно
    /// (и нельзя) отдельно добавлять "зеркальный" вклад для (col, row),
    /// иначе значение задвоится. Если локальная матрица элемента сама
    /// симметрична (как обычно в МКЭ), при сборке добавляйте внедиагональный
    /// вклад один раз, а не для обеих сторон пары индексов.
    /// </summary>
    public class SymmetricSparseMatrixCSR
    {
        private double[] values;      // Значения хранимой половины (row <= col)
        private int[] colIndices;     // Индексы столбцов
        private int[] rowPointers;    // Указатели на начало строк

        private Dictionary<(int row, int col), double> assemblyBuffer;

        private readonly int size;
        private int nonZeroCount;
        private bool isFinalized;

        private const double Tolerance = 1e-15;

        public SymmetricSparseMatrixCSR(int size)
        {
            this.size = size;
            assemblyBuffer = new Dictionary<(int, int), double>();
            isFinalized = false;
        }

        /// <summary>
        /// Построение сразу из списка вкладов (координатный формат).
        /// Повторяющиеся нормализованные (row, col) суммируются.
        /// </summary>
        public SymmetricSparseMatrixCSR(int size, IEnumerable<(int row, int col, double value)> elements)
            : this(size)
        {
            foreach (var e in elements)
                AddToElement(e.row, e.col, e.value);

            FinalizeAssembly();
        }

        /// <summary>
        /// Добавляет вклад к элементу матрицы (аналог K[i,j] += local[i,j] при сборке МКЭ).
        /// (row, col) нормализуются к (min, max) — добавлять вклад нужно один раз,
        /// не дублируя его для переставленных индексов.
        /// </summary>
        public void AddToElement(int row, int col, double value)
        {
            CheckBounds(row, col);
            Normalize(ref row, ref col);

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
        /// Завершает сборку: строит компактные CSR-массивы (только верхний треугольник)
        /// из накопленного буфера и фиксирует структуру разреженности.
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
            rowPointers = new int[size + 1];

            int currentIndex = 0;
            for (int row = 0; row < size; row++)
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

            rowPointers[size] = currentIndex;
        }

        /// <summary>
        /// Прямой доступ к элементу матрицы по глобальным индексам (в любом порядке —
        /// индексы нормализуются). Запись после финализации возможна только
        /// в уже существующую ненулевую позицию.
        /// </summary>
        public double this[int row, int col]
        {
            get
            {
                CheckBounds(row, col);
                Normalize(ref row, ref col);

                if (!isFinalized)
                    return assemblyBuffer.TryGetValue((row, col), out double v) ? v : 0.0;

                int index = FindIndex(row, col);
                return index >= 0 ? values[index] : 0.0;
            }
            set
            {
                CheckBounds(row, col);
                Normalize(ref row, ref col);

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

        private static void Normalize(ref int row, ref int col)
        {
            if (row > col)
            {
                int tmp = row;
                row = col;
                col = tmp;
            }
        }

        private void CheckBounds(int row, int col)
        {
            if (row < 0 || row >= size || col < 0 || col >= size)
                throw new IndexOutOfRangeException($"Индексы вне диапазона: ({row}, {col})");
        }

        /// <summary>
        /// Бинарный поиск нормализованной позиции (row &lt;= col) в отсортированном
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
        /// Умножение матрицы на вектор. Поскольку хранится только верхний
        /// треугольник, каждый внедиагональный элемент даёт вклад сразу
        /// в две позиции результата — в свою строку и в симметричную.
        /// </summary>
        public double[] Multiply(double[] vector)
        {
            RequireFinalized();

            if (vector.Length != size)
                throw new ArgumentException($"Размер вектора {vector.Length} не соответствует размеру матрицы {size}");

            var result = new double[size];

            for (int row = 0; row < size; row++)
            {
                int start = rowPointers[row];
                int end = rowPointers[row + 1];

                for (int k = start; k < end; k++)
                {
                    int col = colIndices[k];
                    double v = values[k];

                    result[row] += v * vector[col];
                    if (col != row)
                        result[col] += v * vector[row];
                }
            }

            return result;
        }

        /// <summary>
        /// Логическая строка матрицы (индекс столбца -> значение), включая
        /// элементы "нижнего" треугольника, восстановленные по симметрии из
        /// столбцов предыдущих строк. Стоимость вызова — O(row) на восстановление
        /// нижней части; не предназначен для построчного обхода всей матрицы
        /// (для этого используйте Multiply).
        /// </summary>
        public Dictionary<int, double> GetRow(int row)
        {
            RequireFinalized();

            if (row < 0 || row >= size)
                throw new IndexOutOfRangeException($"Индекс строки {row} вне диапазона");

            var rowElements = new Dictionary<int, double>();

            // Собственный сегмент строки содержит все (row, col) с col >= row.
            for (int k = rowPointers[row]; k < rowPointers[row + 1]; k++)
                rowElements[colIndices[k]] = values[k];

            // Элементы (row, col) с col < row восстанавливаются из
            // симметричных позиций (col, row).
            for (int col = 0; col < row; col++)
            {
                int index = FindIndex(col, row);
                if (index >= 0)
                    rowElements[col] = values[index];
            }

            return rowElements;
        }

        public int NonZeroCount => isFinalized
            ? nonZeroCount
            : assemblyBuffer.Count(kv => Math.Abs(kv.Value) > Tolerance);

        public int Size => size;

        public bool IsFinalized => isFinalized;

        public bool IsZero(int row, int col)
        {
            CheckBounds(row, col);
            Normalize(ref row, ref col);

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
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                    Console.Write($"{this[i, j],8:F3} ");

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Вывод внутреннего представления CSR (только хранимая половина).
        /// </summary>
        public void PrintCSR()
        {
            RequireFinalized();

            Console.WriteLine("Symmetric CSR Representation (хранится только верхний треугольник):");
            Console.WriteLine($"Size: {size}, NonZero (в хранимой половине): {nonZeroCount}");
            Console.WriteLine($"RowPointers: [{string.Join(", ", rowPointers)}]");
            Console.WriteLine($"ColIndices:  [{string.Join(", ", colIndices)}]");
            Console.WriteLine($"Values:      [{string.Join(", ", values.Select(v => v.ToString("F3")))}]");
        }
    }
}
