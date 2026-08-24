namespace CAESolvers
{
    public abstract class IterativeSolver<TMatrix, TResult>
        : ILinearSolver<TMatrix>
        where TMatrix : class, ICsrMatrix
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

        public abstract double[] Solve(LinearSystem<TMatrix> system);

        protected double CalculateNorm(double[] vector)
        {
            return residualCalculator.CalculateNorm(vector);
        }

        protected void ValidateCommonArguments(LinearSystem<TMatrix> system)
        {
            ArgumentNullException.ThrowIfNull(system);

            if (!double.IsFinite(RelativeTolerance) ||
                RelativeTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(RelativeTolerance));
            }
        }
    }
}
