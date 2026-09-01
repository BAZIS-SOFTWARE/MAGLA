namespace CAESolvers
{
    public abstract class IterativeSolver<TMatrix, TResult>
        : LinearSolver<TMatrix>
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

        protected double CalculateNorm(double[] vector)
        {
            return residualCalculator.CalculateNorm(vector);
        }

        protected void ValidateCommonArguments()
        {
            if (!double.IsFinite(RelativeTolerance) ||
                RelativeTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(RelativeTolerance));
            }
        }
    }
}
