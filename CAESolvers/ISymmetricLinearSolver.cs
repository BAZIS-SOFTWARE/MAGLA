namespace CAESolvers
{
    /// <summary>
    /// Контракт решателя симметричной системы. Не требует хранить матрицу
    /// или факторизацию между вызовами.
    /// </summary>
    public interface ISymmetricLinearSolver
        : ILinearSolver<SymmetricCSRMatrix>
    {
    }
}
