namespace CAESolvers
{
    /// <summary>
    /// Базовый класс прямого решателя линейной системы.
    /// </summary>
    public abstract class DirectLinearSolver<TMatrix> : LinearSolver<TMatrix> where TMatrix : class, ICsrMatrix
    {
    }
}
