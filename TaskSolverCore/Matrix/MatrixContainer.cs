using CAESolvers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TaskSolverCore.Matrix
{
    public enum MatrixType
    {
        stifness, heatTransfer, heatCapacity, heatTransferCapacity
    }

    /// <summary>Контейнер CSR-матриц расчётной задачи.</summary>
    public class MatrixContainer
    {
        private readonly Dictionary<MatrixType, ICsrMatrix> matrices = new();

        public ICsrMatrix this[MatrixType type] => matrices[type];

        public void AddMatrix(MatrixType type, ICsrMatrix matrix)
        {
            matrices.Add(type, matrix ?? throw new ArgumentNullException(nameof(matrix)));
        }

        public virtual TMatrix Get<TMatrix>(MatrixType type)
            where TMatrix : class, ICsrMatrix
        {
            if (!matrices.TryGetValue(type, out ICsrMatrix? matrix))
                throw new KeyNotFoundException($"Matrix {type} is missing from the container.");

            if (matrix is TMatrix typedMatrix)
                return typedMatrix;

            throw new InvalidOperationException(
                $"Matrix {type} has type {matrix.GetType().Name}; " +
                $"expected {typeof(TMatrix).Name}.");
        }

        public void ClearMatrixes()
        {
            foreach (ICsrMatrix matrix in matrices.Values)
                matrix.ClearValues();
        }

        public void ClearMatrix(MatrixType type)
        {
            matrices[type].ClearValues();
        }
    }

    /// <summary>
    /// Временная совместимость со старым контуром сборки матриц. После его
    /// перевода на CSR этот класс следует удалить.
    /// </summary>
    public class MatrixContainer<T> : MatrixContainer where T : INumber<T>
    {
        private readonly Dictionary<MatrixType, MatrixNumeric<T>> numericMatrices = new();

        public new MatrixNumeric<T> this[MatrixType type] => numericMatrices[type];

        public override TMatrix Get<TMatrix>(MatrixType type)
        {
            if (typeof(T) == typeof(double) &&
                typeof(TMatrix) == typeof(SymmetricCSRMatrix) &&
                numericMatrices.TryGetValue(type, out MatrixNumeric<T>? source))
            {
                var builder = new SymmetricCSRMatrixBuilder(source.Length);

                for (int row = 0; row < source.Length; row++)
                {
                    foreach (int col in source.R_Inds[row])
                    {
                        if (col >= row)
                        {
                            double value = double.CreateChecked(source[row, col]);
                            builder.AddToElement(row, col, value);
                        }
                    }
                }

                return (TMatrix)(object)builder.Build();
            }

            return base.Get<TMatrix>(type);
        }

        public new void ClearMatrixes()
        {
            foreach (MatrixNumeric<T> matrix in numericMatrices.Values)
                matrix.Clear();
        }

        public void AddMatrix(MatrixType type, MatrixNumeric<T> matrix)
        {
            numericMatrices.Add(type, matrix);
        }

        public new void ClearMatrix(MatrixType type)
        {
            numericMatrices[type].Clear();
        }
    }
}
