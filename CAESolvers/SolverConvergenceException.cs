namespace CAESolvers
{
    using System;

    /// <summary>
    /// Исключение, возникающее при вызове итерационного решателя через
    /// <see cref="ISymmetricLinearSolver"/>, если заданная точность не была
    /// достигнута.
    /// </summary>
    public sealed class SolverConvergenceException : InvalidOperationException
    {
        public SolverConvergenceException(int iterations, double residualNorm)
            : base(
                $"Решатель не сошёлся за {iterations} итераций. " +
                $"Норма невязки: {residualNorm:E6}.")
        {
            Iterations = iterations;
            ResidualNorm = residualNorm;
        }

        /// <summary>Число выполненных итераций.</summary>
        public int Iterations { get; }

        /// <summary>Норма невязки на момент останова.</summary>
        public double ResidualNorm { get; }
    }
}
