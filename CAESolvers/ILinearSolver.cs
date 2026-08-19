namespace CAESolvers
{
    /// <summary>Тонкий контракт решателя линейной системы A x = b.</summary>
    public interface ILinearSolver<in TMatrix> where TMatrix : ICsrMatrix
    {
        /// <summary>Решает систему A x = b.</summary>
        double[] Solve(TMatrix matrix, double[] rightHandSide);
    }
}
