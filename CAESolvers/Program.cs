using CAESolvers;

/// <summary>
/// Точка входа: запуск замера производительности прямого решателя U^T D U.
///
/// Использование:
///   dotnet run -c Release                       — задача ~300 000 уравнений
///   dotnet run -c Release -- 50000              — другое число уравнений
///   dotnet run -c Release -- 300000 3 1,2,4,8   — уравнений, ст. свободы, числа потоков
///
/// Замер обязательно запускать в конфигурации Release: в Debug оптимизации
/// отключены, и векторные ядра факторизации теряют в скорости в разы.
/// </summary>
internal class Program
{
    private static void Main(string[] args)
    {
        int equations = args.Length > 0 ? int.Parse(args[0]) : 300_000;
        int degreesOfFreedom = args.Length > 1 ? int.Parse(args[1]) : 3;
        int[]? threadCounts = args.Length > 2
            ? Array.ConvertAll(args[2].Split(','), int.Parse)
            : null;

        UtduSolverBenchmark.Run(equations, degreesOfFreedom, threadCounts);
    }
}
