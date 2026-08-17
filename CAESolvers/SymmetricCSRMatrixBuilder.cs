namespace CAESolvers
{
    /// <summary>
    /// Накопитель вкладов для сборки <see cref="SymmetricCSRMatrix"/>
    /// (аналог K[i,j] += local[i,j] при сборке МКЭ). Индексы (row, col)
    /// нормализуются к (min, max), поэтому каждый физический вклад нужно
    /// добавлять РОВНО ОДИН РАЗ — не нужно (и нельзя) отдельно добавлять
    /// "зеркальный" вклад для (col, row), иначе значение задвоится.
    /// Когда сборка завершена, <see cref="Build"/> строит готовую матрицу.
    /// </summary>
    public class SymmetricCSRMatrixBuilder
    {
        private readonly Dictionary<MatrixPosition, double> buffer = new Dictionary<MatrixPosition, double>();

        private readonly int size;

        public SymmetricCSRMatrixBuilder(int size)
        {
            this.size = size;
        }

        public void AddToElement(int row, int col, double value)
        {
            if (row < 0 || row >= size || col < 0 || col >= size)
                throw new IndexOutOfRangeException($"Индексы вне диапазона: ({row}, {col})");

            Normalize(ref row, ref col);

            var position = new MatrixPosition(row, col);
            buffer.TryGetValue(position, out double existing);
            buffer[position] = existing + value;
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

        /// <summary>
        /// Строит готовую матрицу из накопленных вкладов.
        /// </summary>
        public SymmetricCSRMatrix Build()
        {
            var elements = buffer.Select(kv => new MatrixEntry(kv.Key, kv.Value));
            return new SymmetricCSRMatrix(size, elements);
        }
    }
}