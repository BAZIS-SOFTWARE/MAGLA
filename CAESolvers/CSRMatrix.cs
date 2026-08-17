namespace CAESolvers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;


    /// <summary>
    /// Разреженная неизменяемая матрица в формате CSR для хранения матриц МКЭ.
    /// Строится один раз из координатного формата (см. <see cref="SparseMatrixCSRBuilder"/>
    /// для инкрементальной сборки с накоплением вкладов). После построения
    /// структуру разреженности изменить нельзя — доступ по индексам ищется
    /// бинарным поиском по отсортированным столбцам строки (O(log k), k —
    /// число ненулевых в строке); можно только обновить значение уже
    /// существующей ненулевой позиции (<see cref="AccumulateAt"/>, this[,]).
    /// </summary>
    public class CSRMatrix
    {
        private readonly double[] values;      // Значения ненулевых элементов
        private readonly int[] colIndices;     // Индексы столбцов
        private readonly int[] rowPointers;    // Указатели на начало строк
        private readonly int[] diagonalIndices; // diagonalIndices[row] -> позиция A[row,row], либо -1

        private readonly int rows;
        private readonly int cols;
        private readonly int nonZeroCount;

        private const double Tolerance = 1e-15;

        /// <summary>
        /// Строит матрицу из списка вкладов в координатном формате (COO).
        /// Повторяющиеся (Row, Col) суммируются, как при сборке МКЭ.
        /// </summary>
        public CSRMatrix(int rows, int cols, IEnumerable<MatrixEntry> elements)
        {
            this.rows = rows;
            this.cols = cols;

            var byRow = elements
                .GroupBy(e => e.Position)
                .Select(g => new MatrixEntry(g.Key, g.Sum(e => e.Value)))
                .Where(e => Math.Abs(e.Value) > Tolerance)
                .GroupBy(e => e.Row)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Col).ToList());

            nonZeroCount = byRow.Sum(g => g.Value.Count);
            values = new double[nonZeroCount];
            colIndices = new int[nonZeroCount];
            rowPointers = new int[rows + 1];
            diagonalIndices = new int[rows];
            for (int i = 0; i < rows; i++)
                diagonalIndices[i] = -1;

            int currentIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                rowPointers[row] = currentIndex;

                if (byRow.TryGetValue(row, out var items))
                {
                    foreach (var item in items)
                    {
                        values[currentIndex] = item.Value;
                        colIndices[currentIndex] = item.Col;

                        if (item.Col == row)
                            diagonalIndices[row] = currentIndex;

                        currentIndex++;
                    }
                }
            }

            rowPointers[rows] = currentIndex;
        }

        /// <summary>
        /// Прибавляет значение к уже существующей ненулевой позиции матрицы
        /// (например, при повторной сборке на новой итерации решателя).
        /// Новую ненулевую позицию завести нельзя — структура разреженности
        /// зафиксирована при построении.
        /// </summary>
        public void AccumulateAt(int row, int col, double value)
        {
            CheckBounds(row, col);

            int index = FindIndex(row, col);
            if (index < 0)
                throw new InvalidOperationException(
                    $"Позиция ({row}, {col}) отсутствует в структуре разреженности. " +
                    "Включите эту позицию в сборку через SparseMatrixCSRBuilder заранее.");

            values[index] += value;
        }

        /// <summary>
        /// Прямой доступ к элементу матрицы по глобальным индексам.
        /// Запись возможна только в уже существующую ненулевую позицию.
        /// </summary>
        public double this[int row, int col]
        {
            get
            {
                CheckBounds(row, col);

                int index = FindIndex(row, col);
                return index >= 0 ? values[index] : 0.0;
            }
            set
            {
                CheckBounds(row, col);

                int index = FindIndex(row, col);
                if (index < 0)
                    throw new InvalidOperationException(
                        $"Позиция ({row}, {col}) отсутствует в структуре разреженности.");

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
        /// Диагональ (row == col) отдаётся за O(1) через diagonalIndices.
        /// </summary>
        private int FindIndex(int row, int col)
        {
            if (row == col)
                return diagonalIndices[row];

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
        /// Диагональный элемент A[row,row] за O(1) — без бинарного поиска.
        /// Удобно для итерационных решателей и предобуславливателей
        /// (Якоби, Гаусс-Зейдель, SSOR, диагональное масштабирование),
        /// которые обращаются к диагонали на каждой итерации.
        /// </summary>
        public double GetDiagonal(int row)
        {
            if (row < 0 || row >= rows)
                throw new IndexOutOfRangeException($"Индекс строки {row} вне диапазона");

            int index = diagonalIndices[row];
            return index >= 0 ? values[index] : 0.0;
        }

        /// <summary>
        /// Умножение матрицы на вектор.
        /// </summary>
        public double[] Multiply(double[] vector)
        {
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
            if (row < 0 || row >= rows)
                throw new IndexOutOfRangeException($"Индекс строки {row} вне диапазона");

            var rowElements = new Dictionary<int, double>();
            int start = rowPointers[row];
            int end = rowPointers[row + 1];

            for (int i = start; i < end; i++)
                rowElements[colIndices[i]] = values[i];

            return rowElements;
        }

        public int NonZeroCount => nonZeroCount;

        public (int rows, int cols) Size => (rows, cols);

        public bool IsZero(int row, int col)
        {
            CheckBounds(row, col);
            return FindIndex(row, col) < 0;
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
            Console.WriteLine("CSR Representation:");
            Console.WriteLine($"Rows: {rows}, Cols: {cols}, NonZero: {nonZeroCount}");
            Console.WriteLine($"RowPointers: [{string.Join(", ", rowPointers)}]");
            Console.WriteLine($"ColIndices:  [{string.Join(", ", colIndices)}]");
            Console.WriteLine($"Values:      [{string.Join(", ", values.Select(v => v.ToString("F3")))}]");
        }
    }
}
