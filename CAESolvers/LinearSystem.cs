namespace CAESolvers
{
    /// <summary>
    /// Линейная система с CSR-матрицей.
    /// </summary>
    public sealed class LinearSystem
    {
        public LinearSystem(ICsrMatrix matrix, double[] rightHandSide)
        {
            Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
            RightHandSide = rightHandSide ?? throw new ArgumentNullException(nameof(rightHandSide));

            if (matrix.RowCount != rightHandSide.Length)
            {
                throw new ArgumentException("The right-hand side length does not match the matrix row count.", nameof(rightHandSide));
            }
        }

        public ICsrMatrix Matrix { get; }

        public double[] RightHandSide { get; }
    }
}
