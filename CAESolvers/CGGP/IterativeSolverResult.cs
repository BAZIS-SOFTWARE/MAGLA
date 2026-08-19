namespace CAESolvers
{
    /// <summary>
    /// Результат работы итерационного решателя СЛАУ: найденное решение,
    /// число выполненных итераций, признак сходимости по заданному критерию
    /// и норма невязки на момент останова (для диагностики/логирования).
    /// </summary>
    public class IterativeSolverResult
    {
        public IterativeSolverResult(
            double[] solution, int iterations, bool converged, double residualNorm)
        {
            Solution = solution;
            Iterations = iterations;
            Converged = converged;
            ResidualNorm = residualNorm;
        }

        /// <summary>Найденное (или последнее полученное) приближение решения x.</summary>
        public double[] Solution { get; }

        /// <summary>Число выполненных итераций.</summary>
        public int Iterations { get; }

        /// <summary>
        /// true, если относительная норма невязки достигла RelativeTolerance до
        /// исчерпания MaxIterations.
        /// </summary>
        public bool Converged { get; }

        /// <summary>Норма невязки ||b - A x|| на момент останова.</summary>
        public double ResidualNorm { get; }
    }
}
