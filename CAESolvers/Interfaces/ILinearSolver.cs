namespace CAESolvers
{
    /// <summary>Общий контракт решателя линейной системы независимо от типа CSR-матрицы.</summary>
    public interface ILinearSolver
    {
        /// <summary>Решает систему A x = b.</summary>
        double[] Solve(LinearSystem system);
    }
}
