namespace CAESolvers
{
    using System;

    /// <summary>
    /// Общий контракт решателя симметричной системы линейных уравнений
    /// A x = b. Контракт не требует хранить матрицу или факторизацию между
    /// вызовами: конкретный решатель сам определяет свой жизненный цикл.
    /// </summary>
    public interface ISymmetricLinearSolver
    {
        /// <summary>Решает систему A x = b.</summary>
        /// <exception cref="ArgumentNullException">
        /// Матрица или правая часть равна <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Размер правой части не соответствует размеру матрицы.
        /// </exception>
        double[] Solve(SymmetricCSRMatrix matrix, double[] rightHandSide);
    }

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
