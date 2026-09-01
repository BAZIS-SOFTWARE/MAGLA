namespace CAESolvers
{
    /// <summary>
    /// Общий контракт полной и симметричной матриц в формате CSR.
    /// </summary>
    public interface ICsrMatrix
    {
        int RowCount { get; }

        int ColumnCount { get; }

        int NonZeroCount { get; }

        double this[int row, int col] { get; set; }

        void AccumulateAt(int row, int col, double value);

        double[] Multiply(double[] vector);

        void LineCross(double[] rightHandSide, double prescribedValue, int index);

        /// <summary>
        /// Обнуляет численные значения, сохраняя структуру CSR.
        /// </summary>
        void ClearValues();
    }
}
