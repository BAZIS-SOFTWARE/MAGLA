namespace CAESolvers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    

    /// <summary>
    /// Разреженная неизменяемая симметричная матрица (A[i,j] == A[j,i]) в
    /// формате CSR, хранящая только верхний треугольник (row &lt;= col)
    /// вместе с диагональю. Подходит для задач упругости, теплопроводности
    /// и т.п., где матрица жёсткости симметрична, и экономит примерно вдвое
    /// память и число операций по сравнению с общим <see cref="CSRMatrix"/>.
    /// Строится один раз из координатного формата (см.
    /// <see cref="SymmetricSparseMatrixCSRBuilder"/> для инкрементальной
    /// сборки с накоплением вкладов).
    /// </summary>
    public class SymmetricCSRMatrix : ICsrMatrix
    {
        private readonly double[] values;      // Значения хранимой половины (row <= col)
        private readonly int[] colIndices;     // Индексы столбцов
        private readonly int[] rowPointers;    // Указатели на начало строк
        private readonly int[] diagonalIndices; // diagonalIndices[row] -> позиция A[row,row], либо -1

        private readonly int size;
        private readonly int nonZeroCount;

        private const double Tolerance = 1e-15;

        /// <summary>
        /// Строит матрицу из списка вкладов в координатном формате (COO).
        /// Индексы каждого вклада нормализуются к (min, max), повторяющиеся
        /// нормализованные (Row, Col) суммируются.
        /// </summary>
        public SymmetricCSRMatrix(int size, IEnumerable<MatrixEntry> elements)
        {
            this.size = size;

            var normalized = elements.Select(e =>
            {
                int row = e.Row;
                int col = e.Col;
                if (row > col)
                {
                    int tmp = row;
                    row = col;
                    col = tmp;
                }

                return new MatrixEntry(row, col, e.Value);
            });

            var byRow = normalized
                .GroupBy(e => e.Position)
                .Select(g => new MatrixEntry(g.Key, g.Sum(e => e.Value)))
                .Where(e => Math.Abs(e.Value) > Tolerance)
                .GroupBy(e => e.Row)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Col).ToList());

            nonZeroCount = byRow.Sum(g => g.Value.Count);
            values = new double[nonZeroCount];
            colIndices = new int[nonZeroCount];
            rowPointers = new int[size + 1];
            diagonalIndices = new int[size];
            for (int i = 0; i < size; i++)
                diagonalIndices[i] = -1;

            int currentIndex = 0;
            for (int row = 0; row < size; row++)
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

            rowPointers[size] = currentIndex;
        }

        /// <summary>
        /// Прибавляет значение к уже существующей ненулевой позиции матрицы
        /// (например, при повторной сборке на новой итерации решателя).
        /// (row, col) нормализуются к (min, max). Новую ненулевую позицию
        /// завести нельзя — структура разреженности зафиксирована при построении.
        /// </summary>
        public void AccumulateAt(int row, int col, double value)
        {
            CheckBounds(row, col);
            Normalize(ref row, ref col);

            int index = FindIndex(row, col);
            if (index < 0)
                throw new InvalidOperationException(
                    $"Position ({row}, {col}) is absent from the sparse structure. " +
                    "Add this position to SymmetricSparseMatrixCSRBuilder before assembly.");

            values[index] += value;
        }

        /// <summary>Обнуляет значения, не изменяя структуру CSR.</summary>
        public void ClearValues()
        {
            Array.Clear(values);
        }

        /// <summary>
        /// Накладывает существенное (Дирихле) граничное условие
        /// x[index] = prescribedValue на систему A x = rightHandSide.
        /// Связи с исключаемой неизвестной зануляются, диагональный элемент
        /// заменяется единицей, а правая часть остальных уравнений
        /// корректируется. Структура CSR при этом не изменяется.
        /// </summary>
        public void LineCross(double[] rightHandSide, double prescribedValue, int index)
        {
            if (rightHandSide == null)
                throw new ArgumentNullException(nameof(rightHandSide));
            if (rightHandSide.Length != size)
                throw new ArgumentException(
                    $"The right-hand side length {rightHandSide.Length} does not match the matrix size {size}.",
                    nameof(rightHandSide));
            if (index < 0 || index >= size)
                throw new ArgumentOutOfRangeException(nameof(index));

            int diagonalIndex = diagonalIndices[index];
            if (diagonalIndex < 0)
                throw new InvalidOperationException(
                    $"Diagonal position ({index}, {index}) is absent from the sparse structure.");

            for (int row = 0; row < size; row++)
            {
                int start = rowPointers[row];
                int end = rowPointers[row + 1];

                for (int position = start; position < end; position++)
                {
                    int col = colIndices[position];
                    if (row == col)
                        continue;

                    int other;
                    if (row == index)
                        other = col;
                    else if (col == index)
                        other = row;
                    else
                        continue;

                    rightHandSide[other] -= values[position] * prescribedValue;
                    values[position] = 0.0;
                }
            }

            values[diagonalIndex] = 1.0;
            rightHandSide[index] = prescribedValue;
        }

        /// <summary>
        /// Прямой доступ к элементу матрицы по глобальным индексам (в любом
        /// порядке — индексы нормализуются). Запись возможна только в уже
        /// существующую ненулевую позицию.
        /// </summary>
        public double this[int row, int col]
        {
            get
            {
                CheckBounds(row, col);
                Normalize(ref row, ref col);

                int index = FindIndex(row, col);
                return index >= 0 ? values[index] : 0.0;
            }
            set
            {
                CheckBounds(row, col);
                Normalize(ref row, ref col);

                int index = FindIndex(row, col);
                if (index < 0)
                    throw new InvalidOperationException(
                        $"Position ({row}, {col}) is absent from the sparse structure.");

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
                throw new IndexOutOfRangeException($"Indices are out of range: ({row}, {col}).");
        }

        /// <summary>
        /// Бинарный поиск нормализованной позиции (row &lt;= col) в отсортированном
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
            if (row < 0 || row >= size)
                throw new IndexOutOfRangeException($"Row index {row} is out of range.");

            int index = diagonalIndices[row];
            return index >= 0 ? values[index] : 0.0;
        }

        /// <summary>
        /// Умножение матрицы на вектор. Поскольку хранится только верхний
        /// треугольник, каждый внедиагональный элемент даёт вклад сразу
        /// в две позиции результата — в свою строку и в симметричную.
        /// </summary>
        public double[] Multiply(double[] vector)
        {
            if (vector.Length != size)
                throw new ArgumentException($"Vector length {vector.Length} does not match the matrix size {size}.");

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
        /// Умножение матрицы на вектор без выделения памяти: result = A * x.
        /// Как и в аллоцирующей перегрузке, каждый внедиагональный элемент
        /// даёт вклад сразу в две позиции результата — в свою строку и в
        /// симметричную. Именно эту перегрузку используют итерационные
        /// решатели: на десятках тысяч итераций аллокация массива результата
        /// на каждое умножение сопоставима по стоимости с самим умножением.
        /// </summary>
        public void Multiply(double[] x, double[] result)
        {
            if (x == null)
                throw new ArgumentNullException(nameof(x));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (x.Length != size)
                throw new ArgumentException($"Vector length {x.Length} does not match the matrix size {size}.");
            if (result.Length != size)
                throw new ArgumentException($"Result length {result.Length} does not match the matrix size {size}.");

            Array.Clear(result, 0, size);

            for (int row = 0; row < size; row++)
            {
                double xrow = x[row];
                double accumulated = 0.0;
                int end = rowPointers[row + 1];

                for (int k = rowPointers[row]; k < end; k++)
                {
                    int col = colIndices[k];
                    double v = values[k];

                    accumulated += v * x[col];
                    if (col != row)
                        result[col] += v * xrow;
                }

                result[row] += accumulated;
            }
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
            if (row < 0 || row >= size)
                throw new IndexOutOfRangeException($"Row index {row} is out of range.");

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

        public int NonZeroCount => nonZeroCount;

        public int RowCount => size;

        public int ColumnCount => size;

        public int Size => size;

        /// <summary>
        /// Указатели начала строк хранимой половины (длина Size + 1) — прямой
        /// доступ только для чтения. Нужен алгоритмам, которым требуется
        /// обойти всю структуру разреженности за O(nnz): символьная фаза
        /// прямых решателей, переупорядочивание для снижения заполнения.
        /// Через индексатор или <see cref="GetRow"/> такой обход выродился бы
        /// в O(n^2) и на матрицах в сотни тысяч уравнений стал бы неприемлем.
        /// </summary>
        public ReadOnlySpan<int> RowPointers => rowPointers;

        /// <summary>
        /// Индексы столбцов хранимой половины (row &lt;= col), внутри каждой
        /// строки — строго по возрастанию. Первый элемент сегмента строки row —
        /// это диагональ A[row,row], если она структурно ненулевая.
        /// </summary>
        public ReadOnlySpan<int> ColumnIndices => colIndices;

        /// <summary>
        /// Значения хранимой половины в том же порядке, что и
        /// <see cref="ColumnIndices"/>. Span всегда отражает текущее состояние
        /// матрицы (значения меняются через <see cref="AccumulateAt"/> и
        /// индексатор при неизменной структуре) — именно это позволяет прямому
        /// решателю один раз выполнить символьную фазу и переиспользовать её
        /// при повторных сборках на новых итерациях.
        /// </summary>
        public ReadOnlySpan<double> Values => values;

        public bool IsZero(int row, int col)
        {
            CheckBounds(row, col);
            Normalize(ref row, ref col);
            return FindIndex(row, col) < 0;
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
            Console.WriteLine("Symmetric CSR Representation (хранится только верхний треугольник):");
            Console.WriteLine($"Size: {size}, NonZero (в хранимой половине): {nonZeroCount}");
            Console.WriteLine($"RowPointers: [{string.Join(", ", rowPointers)}]");
            Console.WriteLine($"ColIndices:  [{string.Join(", ", colIndices)}]");
            Console.WriteLine($"Values:      [{string.Join(", ", values.Select(v => v.ToString("F3")))}]");
        }
    }
}
