namespace CAESolvers
{
    /// <summary>
    /// Общий контракт линейной системы независимо от типа CSR-матрицы.
    /// </summary>
    public interface ILinearSystem
    {
        ICsrMatrix Matrix { get; }

        double[] RightHandSide { get; }
    }
}
