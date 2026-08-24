namespace CAESolvers
{
    /// <summary>
    /// Линейная система с конкретным типом CSR-матрицы.
    /// </summary>
    /// <typeparam name="TMatrix">Тип матрицы системы.</typeparam>
    public sealed class LinearSystem<TMatrix> : ILinearSystem where TMatrix : class, ICsrMatrix
    {
        public LinearSystem(TMatrix matrix, double[] rightHandSide)
        {
            Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
            RightHandSide = rightHandSide ?? throw new ArgumentNullException(nameof(rightHandSide));

            if (matrix.RowCount != rightHandSide.Length)
            {
                throw new ArgumentException("Размер правой части не соответствует числу строк матрицы.", nameof(rightHandSide));
            }
        }

        public TMatrix Matrix { get; }

        public double[] RightHandSide { get; }

        ICsrMatrix ILinearSystem.Matrix => Matrix;
    }
}
