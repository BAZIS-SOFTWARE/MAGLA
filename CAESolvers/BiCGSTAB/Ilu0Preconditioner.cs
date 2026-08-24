namespace CAESolvers
{
    /// <summary>
    /// Неполное LU-разложение без заполнения для общей квадратной CSR-матрицы.
    /// Нижний треугольник L имеет единичную диагональ, верхний треугольник U
    /// хранится вместе с L в структуре разреженности исходной матрицы.
    /// </summary>
    public sealed class Ilu0Preconditioner
    {
        private readonly int[] diagonalIndices;
        private readonly double[] factors;

        /// <summary>Строит ILU(0)-факторизацию указанной матрицы.</summary>
        public Ilu0Preconditioner(CSRMatrix matrix, double pivotTolerance = 1e-30)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            if (matrix.RowCount != matrix.ColumnCount)
                throw new ArgumentException("ILU(0) применим только к квадратной матрице.", nameof(matrix));
            if (!double.IsFinite(pivotTolerance) || pivotTolerance <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(pivotTolerance));

            Matrix = matrix;
            PivotTolerance = pivotTolerance;
            Size = matrix.RowCount;
            factors = matrix.Values.ToArray();
            diagonalIndices = FindDiagonalIndices();

            Factorize();
        }

        /// <summary>
        /// Матрица, для которой построен предобуславливатель. После изменения
        /// численных значений матрицы факторизацию необходимо построить заново.
        /// </summary>
        public CSRMatrix Matrix { get; }

        /// <summary>Размер факторизации.</summary>
        public int Size { get; }

        /// <summary>Минимально допустимый по модулю ведущий элемент U.</summary>
        public double PivotTolerance { get; }

        /// <summary>Вычисляет result = (L U)^(-1) rightHandSide.</summary>
        public void Apply(double[] rightHandSide, double[] result)
        {
            var workspace = new double[Size];
            Apply(rightHandSide, result, workspace);
        }

        internal void Apply(double[] rightHandSide, double[] result, double[] workspace)
        {
            ArgumentNullException.ThrowIfNull(rightHandSide);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(workspace);

            if (rightHandSide.Length != Size)
                throw new ArgumentException("Размер правой части не соответствует размеру предобуславливателя.", nameof(rightHandSide));
            if (result.Length != Size)
                throw new ArgumentException("Размер результата не соответствует размеру предобуславливателя.", nameof(result));
            if (workspace.Length != Size)
                throw new ArgumentException("Размер рабочего буфера не соответствует размеру предобуславливателя.", nameof(workspace));

            var rowPointers = Matrix.RowPointers;
            var columnIndices = Matrix.ColumnIndices;

            for (var row = 0; row < Size; row++)
            {
                var sum = rightHandSide[row];
                for (var position = rowPointers[row]; position < diagonalIndices[row]; position++)
                    sum -= factors[position] * workspace[columnIndices[position]];

                workspace[row] = sum;
            }

            for (var row = Size - 1; row >= 0; row--)
            {
                var sum = workspace[row];
                for (var position = diagonalIndices[row] + 1; position < rowPointers[row + 1]; position++)
                    sum -= factors[position] * result[columnIndices[position]];

                result[row] = sum / factors[diagonalIndices[row]];
            }
        }

        private int[] FindDiagonalIndices()
        {
            var result = new int[Size];
            var rowPointers = Matrix.RowPointers;
            var columnIndices = Matrix.ColumnIndices;

            for (var row = 0; row < Size; row++)
            {
                result[row] = -1;
                for (var position = rowPointers[row]; position < rowPointers[row + 1]; position++)
                {
                    if (columnIndices[position] == row)
                    {
                        result[row] = position;
                        break;
                    }
                }

                if (result[row] < 0)
                    throw new InvalidOperationException($"В строке {row} отсутствует диагональный элемент, необходимый для ILU(0).");
            }

            return result;
        }

        private void Factorize()
        {
            var rowPointers = Matrix.RowPointers;
            var columnIndices = Matrix.ColumnIndices;

            for (var row = 0; row < Size; row++)
            {
                for (var position = rowPointers[row]; position < diagonalIndices[row]; position++)
                {
                    var column = columnIndices[position];
                    var pivot = factors[diagonalIndices[column]];
                    EnsurePivot(column, pivot);

                    var multiplier = factors[position] / pivot;
                    factors[position] = multiplier;

                    for (var upperPosition = diagonalIndices[column] + 1; upperPosition < rowPointers[column + 1]; upperPosition++)
                    {
                        var targetPosition = FindPosition(row, columnIndices[upperPosition]);
                        if (targetPosition >= 0)
                            factors[targetPosition] -= multiplier * factors[upperPosition];
                    }
                }

                EnsurePivot(row, factors[diagonalIndices[row]]);
            }
        }

        private int FindPosition(int row, int column)
        {
            var rowPointers = Matrix.RowPointers;
            var columnIndices = Matrix.ColumnIndices;
            var left = rowPointers[row];
            var right = rowPointers[row + 1] - 1;

            while (left <= right)
            {
                var middle = left + (right - left) / 2;
                var currentColumn = columnIndices[middle];

                if (currentColumn == column)
                    return middle;
                if (currentColumn < column)
                    left = middle + 1;
                else
                    right = middle - 1;
            }

            return -1;
        }

        private void EnsurePivot(int row, double pivot)
        {
            if (!double.IsFinite(pivot) || Math.Abs(pivot) < PivotTolerance)
                throw new InvalidOperationException($"Нулевой или недопустимый ведущий элемент U в строке {row} при построении ILU(0).");
        }
    }
}
