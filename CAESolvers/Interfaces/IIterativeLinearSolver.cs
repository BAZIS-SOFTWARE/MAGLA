namespace CAESolvers
{
    /// <summary>
    /// Контракт итерационного решателя линейной системы.
    /// </summary>
    public interface IIterativeLinearSolver : ILinearSolver
    {
        /// <summary>
        /// Результат последнего запуска или null, если решение ещё не выполнялось.
        /// </summary>
        IterativeSolverResult? LastResult { get; }
    }
}
