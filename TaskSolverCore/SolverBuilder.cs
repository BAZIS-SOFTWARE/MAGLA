using CAESolvers;
using Project.TaskParameters;

namespace TaskSolverCore
{
    internal static class SolverBuilder
    {
        public static ILinearSolver Create(SolverSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            double tolerance = settings.Precision > 0
                ? settings.Precision
                : 1e-8;
            int maxIterations = settings.MaxIter > 0
                ? settings.MaxIter
                : 10_000;

            if (settings.Solver == "Chol_direct")
            {
                return new SymmetricUtduSolver(new UtduSolverOptions
                {
                    MaxDegreeOfParallelism = ResolveParallelism(settings.Priority)
                });
            }
            else
            {
                return new ConjugateGradientGaussPreSolver
                {
                    RelativeTolerance = tolerance,
                    MaxIterations = settings.MaxIter
                };
            }
        }

        private static int ResolveParallelism(string priority)
        {
            int processorCount = Environment.ProcessorCount;
            int requested = priority switch
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
