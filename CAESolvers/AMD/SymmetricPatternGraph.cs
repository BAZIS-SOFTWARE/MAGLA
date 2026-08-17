namespace CAESolvers
{
    using System;

    /// <summary>
    /// Структура разреженности симметричной матрицы, представленная как
    /// неориентированный граф смежности: для каждой вершины i перечислены все
    /// j != i, для которых A[i,j] != 0. В отличие от
    /// <see cref="SymmetricCSRMatrix"/>, где хранится только верхний
    /// треугольник, здесь каждое ребро присутствует в обоих направлениях —
    /// именно такое представление нужно алгоритмам переупорядочивания
    /// (минимальная степень и т.п.), которым на каждом шаге требуется полный
    /// список соседей вершины.
    ///
    /// Диагональ исключается: петли не влияют на заполнение при исключении
    /// Гаусса и только мешали бы алгоритмам степени.
    /// Списки соседей внутри вершины не отсортированы — ни одному из
    /// потребителей этого класса порядок не важен.
    /// </summary>
    public sealed class SymmetricPatternGraph
    {
        private SymmetricPatternGraph(int size, int[] pointers, int[] neighbors)
        {
            Size = size;
            Pointers = pointers;
            Neighbors = neighbors;
        }

        /// <summary>Число вершин (уравнений).</summary>
        public int Size { get; }

        /// <summary>Указатели начала списков смежности, длина Size + 1.</summary>
        public int[] Pointers { get; }

        /// <summary>
        /// Списки смежности; соседи вершины i лежат в
        /// Neighbors[Pointers[i] .. Pointers[i+1]-1]. Длина массива равна
        /// удвоенному числу внедиагональных ненулевых элементов.
        /// </summary>
        public int[] Neighbors { get; }

        /// <summary>Число рёбер (внедиагональных элементов одной половины).</summary>
        public int EdgeCount => Neighbors.Length / 2;

        /// <summary>Степень вершины — число её соседей, O(1).</summary>
        public int GetDegree(int vertex)
        {
            if ((uint)vertex >= (uint)Size)
                throw new ArgumentOutOfRangeException(nameof(vertex));

            return Pointers[vertex + 1] - Pointers[vertex];
        }

        /// <summary>
        /// Список соседей вершины без копирования — срез внутри Neighbors.
        /// Порядок соседей не гарантирован (см. замечание к <see cref="Neighbors"/>).
        /// </summary>
        public ReadOnlySpan<int> GetNeighbors(int vertex)
        {
            if ((uint)vertex >= (uint)Size)
                throw new ArgumentOutOfRangeException(nameof(vertex));

            int start = Pointers[vertex];
            int end = Pointers[vertex + 1];
            return new ReadOnlySpan<int>(Neighbors, start, end - start);
        }

        /// <summary>
        /// Извлекает граф смежности из симметричной матрицы за O(nnz).
        /// </summary>
        public static SymmetricPatternGraph FromMatrix(SymmetricCSRMatrix matrix)
        {
            if (matrix == null)
                throw new ArgumentNullException(nameof(matrix));

            int n = matrix.Size;
            var rowPointers = matrix.RowPointers;
            var columnIndices = matrix.ColumnIndices;

            // Первый проход: степень каждой вершины. Каждый хранимый
            // внедиагональный элемент (row, col) даёт по ребру в обе стороны.
            var pointers = new int[n + 1];
            for (int row = 0; row < n; row++)
            {
                int end = rowPointers[row + 1];
                for (int k = rowPointers[row]; k < end; k++)
                {
                    int col = columnIndices[k];
                    if (col == row)
                        continue;

                    pointers[row + 1]++;
                    pointers[col + 1]++;
                }
            }

            for (int i = 0; i < n; i++)
                pointers[i + 1] += pointers[i];

            // Второй проход: раскладка соседей. cursor[i] — текущая позиция
            // записи для вершины i.
            var neighbors = new int[pointers[n]];
            var cursor = new int[n];
            Array.Copy(pointers, cursor, n);

            for (int row = 0; row < n; row++)
            {
                int end = rowPointers[row + 1];
                for (int k = rowPointers[row]; k < end; k++)
                {
                    int col = columnIndices[k];
                    if (col == row)
                        continue;

                    neighbors[cursor[row]++] = col;
                    neighbors[cursor[col]++] = row;
                }
            }

            return new SymmetricPatternGraph(n, pointers, neighbors);
        }
    }
}
