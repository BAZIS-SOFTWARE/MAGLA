namespace CAESolvers
{
    /// <summary>
    /// Диагональный предобуславливатель Якоби для симметричной CSR-матрицы:
    /// result = diag(A)^(-1) source.
    /// </summary>
    public sealed class JacobiPreconditioner
    {
        private readonly double[] inverseDiagonal;

        /// <summary>Строит и сохраняет обратную диагональ указанной матрицы.</summary>
        public JacobiPreconditioner(SymmetricCSRMatrix matrix, double pivotTolerance = 1e-300)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            if (!double.IsFinite(pivotTolerance) || pivotTolerance <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(pivotTolerance));

            Matrix = matrix;
            PivotTolerance = pivotTolerance;
            Size = matrix.Size;
            inverseDiagonal = new double[Size];

            for (var index = 0; index < Size; index++)
            {
                var diagonal = matrix.GetDiagonal(index);
                if (!double.IsFinite(diagonal) || Math.Abs(diagonal) <= PivotTolerance)
                    throw new InvalidOperationException($"The diagonal element in row {index} is zero or invalid while building the Jacobi preconditioner.");

                inverseDiagonal[index] = 1.0 / diagonal;
            }
        }

        /// <summary>Матрица, для которой построен предобуславливатель.</summary>
        public SymmetricCSRMatrix Matrix { get; }

        /// <summary>Размер предобуславливателя.</summary>
        public int Size { get; }

        /// <summary>Минимально допустимый по модулю диагональный элемент.</summary>
        public double PivotTolerance { get; }

        /// <summary>Вычисляет result = diag(A)^(-1) source.</summary>
        public void Apply(double[] source, double[] result)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(result);

            if (source.Length != Size)
                throw new ArgumentException("The source vector length does not match the preconditioner size.", nameof(source));
            if (result.Length != Size)
                throw new ArgumentException("The result vector length does not match the preconditioner size.", nameof(result));

            for (var index = 0; index < Size; index++)
                result[index] = inverseDiagonal[index] * source[index];
        }
    }
}
