namespace CAESolvers
{
    /// <summary>
    /// Базовый решатель, проверяющий совместимость фактического типа матрицы
    /// с алгоритмом до запуска типизированного вычислительного ядра.
    /// </summary>
    public abstract class LinearSolver<TMatrix> : ILinearSolver where TMatrix : class, ICsrMatrix
    {
        public double[] Solve(LinearSystem system)
        {
            ArgumentNullException.ThrowIfNull(system);

            var matrix = GetMatrix(system);
            return SolveCore(matrix, system.RightHandSide);
        }

        protected TMatrix GetMatrix(LinearSystem system)
        {
            ArgumentNullException.ThrowIfNull(system);

            if (system.Matrix is not TMatrix matrix)
            {
                throw new ArgumentException(
                    $"The solver supports {typeof(TMatrix).Name}, but {system.Matrix.GetType().Name} was provided.",
                    nameof(system));
            }

            return matrix;
        }

        protected abstract double[] SolveCore(TMatrix matrix, double[] rightHandSide);
    }
}
