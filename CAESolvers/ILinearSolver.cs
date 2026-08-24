namespace CAESolvers
{
    /// <summary>Тонкий контракт решателя линейной системы A x = b.</summary>
    public interface ILinearSolver<TMatrix> where TMatrix : class, ICsrMatrix
    {
        /// <summary>Решает систему A x = b.</summary>
        double[] Solve(LinearSystem<TMatrix> system);
    }
}
