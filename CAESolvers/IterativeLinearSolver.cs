namespace CAESolvers
{
    /// <summary>
    /// Базовый класс итерационного решателя линейной системы.
    /// </summary>
    public abstract class IterativeLinearSolver<TMatrix> : LinearSolver<TMatrix>, IIterativeLinearSolver where TMatrix : class, ICsrMatrix
    {
        private readonly ResidualCalculator residualCalculator = new();

        protected IterativeLinearSolver(int defaultMaxIterations = 0)
        {
            MaxIterations = defaultMaxIterations;
        }

        public double RelativeTolerance { get; set; } = 1e-8;

        public int MaxIterations { get; set; }

        public IterativeSolverResult? LastResult { get; protected set; }

        protected double CalculateNorm(double[] vector)
        {
            return residualCalculator.CalculateNorm(vector);
        }

        protected void ValidateCommonArguments()
        {
            if (!double.IsFinite(RelativeTolerance) || RelativeTolerance < 0.0)
                throw new ArgumentOutOfRangeException(nameof(RelativeTolerance));

            if (MaxIterations < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxIterations), "The maximum iteration count cannot be negative.");
        }
    }
}
