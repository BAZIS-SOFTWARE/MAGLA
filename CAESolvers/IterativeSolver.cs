namespace CAESolvers
{
    public abstract class IterativeSolver<TMatrix, TResult>
        : ILinearSolver<TMatrix>
        where TMatrix : ICsrMatrix
        where TResult : IterativeSolverResult
    {
        private readonly ResidualCalculator residualCalculator = new();

        protected IterativeSolver(int defaultMaxIterations = 0)
        {
            MaxIterations = defaultMaxIterations;
        }

        public double RelativeTolerance { get; set; } = 1e-8;

        public int MaxIterations { get; set; }

        public TResult? LastResult { get; protected set; }

        public abstract double[] Solve(
            TMatrix matrix, double[] rightHandSide);

        protected double CalculateNorm(double[] vector)
        {
            return residualCalculator.CalculateNorm(vector);
        }

        protected void ValidateCommonArguments(
            TMatrix matrix, double[] rightHandSide)
        {
            ArgumentNullException.ThrowIfNull(matrix);
            ArgumentNullException.ThrowIfNull(rightHandSide);

            if (!double.IsFinite(RelativeTolerance) ||
                RelativeTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(RelativeTolerance));
            }

            if (rightHandSide.Length != matrix.RowCount)
            {
                throw new ArgumentException(
                    $"Размер правой части {rightHandSide.Length} " +
                    $"не соответствует числу строк матрицы {matrix.RowCount}.",
                    nameof(rightHandSide));
            }
        }
    }
}
