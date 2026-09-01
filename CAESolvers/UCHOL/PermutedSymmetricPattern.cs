namespace CAESolvers
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Структура разреженности переставленной матрицы P^T A P, подготовленная
    /// для символьной и численной фаз прямого решателя.
    ///
    /// Хранится сразу в двух видах, потому что разным фазам нужен разный:
    /// <list type="bullet">
    /// <item>полная структура по столбцам (оба треугольника, без диагонали) —
    /// для построения дерева исключений и подсчёта длин столбцов множителя;</item>
    /// <item>нижняя часть каждого столбца вместе с индексами значений в
    /// исходной матрице — для сборки фронтальных матриц. Индексы значений, а
    /// не сами значения, позволяют переиспользовать всю символьную фазу после
    /// повторной сборки матрицы с новыми числами при неизменной структуре.</item>
    /// </list>
    /// Внутри столбца индексы строк упорядочены по возрастанию — на этом
    /// основаны и обход «нижней части как хвоста столбца», и слияние
    /// вкладов дочерних фронтов без дополнительной сортировки.
    /// </summary>
    public sealed class PermutedSymmetricPattern
    {
        private PermutedSymmetricPattern(
            int size, int[] pointers, int[] rows, int[] lowerStart,
            int[] lowerValuePointers, int[] lowerValueIndices, int[] diagonalValueIndices)
        {
            Size = size;
            Pointers = pointers;
            Rows = rows;
            LowerStart = lowerStart;
            LowerValuePointers = lowerValuePointers;
            LowerValueIndices = lowerValueIndices;
            DiagonalValueIndices = diagonalValueIndices;
        }

        /// <summary>Число уравнений.</summary>
        public int Size { get; }

        /// <summary>Указатели столбцов полной структуры, длина Size + 1.</summary>
        public int[] Pointers { get; }

        /// <summary>
        /// Индексы строк полной структуры (оба треугольника, без диагонали),
        /// внутри столбца — по возрастанию.
        /// </summary>
        public int[] Rows { get; }

        /// <summary>
        /// LowerStart[j] — позиция в <see cref="Rows"/>, с которой начинаются
        /// строки i &gt; j столбца j (то есть его «нижняя» часть). Поскольку
        /// столбец отсортирован, нижняя часть — это его хвост
        /// [LowerStart[j], Pointers[j+1]).
        /// </summary>
        public int[] LowerStart { get; }

        /// <summary>Указатели нижней части по столбцам, длина Size + 1.</summary>
        public int[] LowerValuePointers { get; }

        /// <summary>
        /// Для t-го элемента нижней части столбца j — индекс соответствующего
        /// значения в SymmetricSparseMatrixCSR.Values. Порядок совпадает с
        /// Rows[LowerStart[j] + t].
        /// </summary>
        public int[] LowerValueIndices { get; }

        /// <summary>
        /// Индекс значения A[j,j] в SymmetricSparseMatrixCSR.Values, либо -1,
        /// если диагональ структурно нулевая.
        /// </summary>
        public int[] DiagonalValueIndices { get; }

        /// <summary>
        /// Строит структуру переставленной матрицы за O(nnz + n log d).
        /// </summary>
        /// <param name="matrix">Исходная симметричная матрица.</param>
        /// <param name="inversePermutation">
        /// inversePermutation[original] — новый номер уравнения.
        /// </param>
        public static PermutedSymmetricPattern Create(SymmetricCSRMatrix matrix, int[] inversePermutation)
        {
            if (matrix == null)
                throw new ArgumentNullException(nameof(matrix));
            if (inversePermutation == null)
                throw new ArgumentNullException(nameof(inversePermutation));

            int n = matrix.Size;
            if (inversePermutation.Length != n)
                throw new ArgumentException(
                    $"Permutation length {inversePermutation.Length} does not match the matrix size {n}.");

            var rowPointers = matrix.RowPointers;
            var columnIndices = matrix.ColumnIndices;

            var pointers = new int[n + 1];
            var lowerValuePointers = new int[n + 1];
            var diagonalValueIndices = new int[n];
            Array.Fill(diagonalValueIndices, -1);

            // Проход 1 — подсчёт длин столбцов.
            for (int row = 0; row < n; row++)
            {
                int permutedRow = inversePermutation[row];
                int end = rowPointers[row + 1];

                for (int k = rowPointers[row]; k < end; k++)
                {
                    int col = columnIndices[k];
                    if (col == row)
                    {
                        diagonalValueIndices[permutedRow] = k;
                        continue;
                    }

                    int permutedCol = inversePermutation[col];
                    pointers[permutedRow + 1]++;
                    pointers[permutedCol + 1]++;
                    lowerValuePointers[Math.Min(permutedRow, permutedCol) + 1]++;
                }
            }

            for (int j = 0; j < n; j++)
            {
                pointers[j + 1] += pointers[j];
                lowerValuePointers[j + 1] += lowerValuePointers[j];
            }

            var rows = new int[pointers[n]];
            var lowerValueIndices = new int[lowerValuePointers[n]];
            var lowerRows = new int[lowerValuePointers[n]];

            var cursor = new int[n];
            var lowerCursor = new int[n];
            Array.Copy(pointers, cursor, n);
            Array.Copy(lowerValuePointers, lowerCursor, n);

            // Проход 2 — раскладка.
            for (int row = 0; row < n; row++)
            {
                int permutedRow = inversePermutation[row];
                int end = rowPointers[row + 1];

                for (int k = rowPointers[row]; k < end; k++)
                {
                    int col = columnIndices[k];
                    if (col == row)
                        continue;

                    int permutedCol = inversePermutation[col];
                    rows[cursor[permutedRow]++] = permutedCol;
                    rows[cursor[permutedCol]++] = permutedRow;

                    int low = Math.Min(permutedRow, permutedCol);
                    int high = permutedRow + permutedCol - low;
                    int position = lowerCursor[low]++;
                    lowerRows[position] = high;
                    lowerValueIndices[position] = k;
                }
            }

            // Сортировка внутри столбцов. Столбцы независимы, поэтому проход
            // распараллеливается без всякой синхронизации.
            Parallel.For(0, n, j =>
            {
                int start = pointers[j];
                int length = pointers[j + 1] - start;
                if (length > 1)
                    Array.Sort(rows, start, length);

                int lowerStartIndex = lowerValuePointers[j];
                int lowerLength = lowerValuePointers[j + 1] - lowerStartIndex;
                if (lowerLength > 1)
                    Array.Sort(lowerRows, lowerValueIndices, lowerStartIndex, lowerLength);
            });

            // Нижняя часть столбца — его хвост в отсортированной структуре.
            var lowerStart = new int[n];
            for (int j = 0; j < n; j++)
                lowerStart[j] = pointers[j + 1] - (lowerValuePointers[j + 1] - lowerValuePointers[j]);

            return new PermutedSymmetricPattern(
                n, pointers, rows, lowerStart, lowerValuePointers, lowerValueIndices, diagonalValueIndices);
        }
    }
}
