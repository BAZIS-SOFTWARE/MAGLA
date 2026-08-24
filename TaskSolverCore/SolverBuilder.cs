using CAESolvers;
using Project.TaskParameters;

namespace TaskSolverCore
{
    /// <summary>
    /// Создаёт решатели линейных систем для CSR-матриц.
    /// </summary>
    public static class SolverBuilder
    {
        /// <summary>
        /// Создаёт встроенный решатель для симметричной матрицы.
        /// </summary>
        public static ILinearSolver<SymmetricCSRMatrix> CreateSymmetric(SolverSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return settings.Solver == "Chol_direct"
                ? CreateSymmetricUtduSolver(settings)
                : CreateConjugateGradientSolver(settings);
        }

        /// <summary>Создаёт встроенный решатель для общей CSR-матрицы.</summary>
        public static ILinearSolver<CSRMatrix> CreateGeneral(SolverSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (!string.Equals(settings.Solver, "BiCGStab", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Решатель '{settings.Solver}' не поддерживает общую CSR-матрицу.");

            var tolerance = settings.Precision > 0 ? settings.Precision : 1e-8;
            return new BiCgStabSolver { RelativeTolerance = tolerance, MaxIterations = settings.MaxIter };
        }

        /// <summary>
        /// Создаёт решатель произвольного типа матрицы через фабрику физического модуля.
        /// </summary>
        public static ILinearSolver<TMatrix> Create<TMatrix>(SolverSettings settings, Func<SolverSettings, ILinearSolver<TMatrix>> solverFactory) where TMatrix : class, ICsrMatrix
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(solverFactory);

            var solver = solverFactory(settings);
            return solver ?? throw new InvalidOperationException($"Фабрика не создала решатель для матрицы {typeof(TMatrix).Name}.");
        }

        private static ILinearSolver<SymmetricCSRMatrix> CreateSymmetricUtduSolver(SolverSettings settings)
        {
            return new SymmetricUtduSolver(new UtduSolverOptions
            {
                MaxDegreeOfParallelism = ResolveParallelism(settings.Priority)
            });
        }

        private static ILinearSolver<SymmetricCSRMatrix> CreateConjugateGradientSolver(SolverSettings settings)
        {
            var tolerance = settings.Precision > 0 ? settings.Precision : 1e-8;

            return new ConjugateGradientGaussPreSolver
            {
                RelativeTolerance = tolerance,
                MaxIterations = settings.MaxIter
            };
        }

        private static int ResolveParallelism(string priority)
        {
            var processorCount = Environment.ProcessorCount;
            var requested = priority switch
            {
                "Низкий" => 1,
                "НижеСреднего" => processorCount / 4,
                "Средний" => processorCount / 2,
                "ВышеСреднего" => processorCount / 2 + processorCount / 3,
                "Высокий" => processorCount,
                _ => processorCount
            };

            return Math.Max(1, requested);
        }
    }
}
