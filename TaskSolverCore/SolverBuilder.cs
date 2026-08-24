using CAESolvers;
using Project.TaskParameters;
using System.Collections.Concurrent;

namespace TaskSolverCore
{
    /// <summary>
    /// Создаёт решатели, зарегистрированные для конкретного типа CSR-матрицы.
    /// </summary>
    public static class SolverBuilder
    {
        static SolverBuilder()
        {
            Register<SymmetricCSRMatrix>("Chol_direct", CreateSymmetricUtduSolver);
            RegisterDefault<SymmetricCSRMatrix>(CreateConjugateGradientSolver);
        }

        /// <summary>
        /// Регистрирует именованную фабрику решателя для типа матрицы.
        /// </summary>
        public static void Register<TMatrix>(string solverName, Func<SolverSettings, ILinearSolver<TMatrix>> factory) where TMatrix : class, ICsrMatrix
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(solverName);
            ArgumentNullException.ThrowIfNull(factory);

            SolverRegistry<TMatrix>.Factories[solverName] = factory;
        }

        /// <summary>
        /// Регистрирует фабрику, используемую при отсутствии именованного решателя.
        /// </summary>
        public static void RegisterDefault<TMatrix>(Func<SolverSettings, ILinearSolver<TMatrix>> factory) where TMatrix : class, ICsrMatrix
        {
            ArgumentNullException.ThrowIfNull(factory);
            SolverRegistry<TMatrix>.DefaultFactory = factory;
        }

        /// <summary>
        /// Создаёт решатель для указанного типа матрицы и настроек.
        /// </summary>
        public static ILinearSolver<TMatrix> Create<TMatrix>(SolverSettings settings) where TMatrix : class, ICsrMatrix
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (!string.IsNullOrWhiteSpace(settings.Solver) && SolverRegistry<TMatrix>.Factories.TryGetValue(settings.Solver, out var factory))
                return factory(settings);

            if (SolverRegistry<TMatrix>.DefaultFactory is { } defaultFactory)
                return defaultFactory(settings);

            throw new NotSupportedException($"Для матрицы {typeof(TMatrix).Name} не зарегистрирован решатель '{settings.Solver}'.");
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

        private static class SolverRegistry<TMatrix> where TMatrix : class, ICsrMatrix
        {
            public static ConcurrentDictionary<string, Func<SolverSettings, ILinearSolver<TMatrix>>> Factories { get; } = new(StringComparer.OrdinalIgnoreCase);

            public static Func<SolverSettings, ILinearSolver<TMatrix>>? DefaultFactory { get; set; }
        }
    }
}
