using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAESolvers
{
    /// <summary>
    /// Позиция элемента матрицы (Row, Col) — ключ для словаря накопления
    /// на этапе сборки. Явно реализует IEquatable, чтобы Dictionary
    /// сравнивал и хешировал по полям, а не через рефлексию.
    /// </summary>
    public readonly struct MatrixPosition : IEquatable<MatrixPosition>
    {
        public int Row { get; }
        public int Col { get; }

        public MatrixPosition(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(MatrixPosition other) => Row == other.Row && Col == other.Col;

        public override bool Equals(object obj) => obj is MatrixPosition other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Row, Col);
    }

    /// <summary>
    /// Вклад в координатном формате (COO): значение элемента в позиции Position.
    /// </summary>
    public readonly struct MatrixEntry
    {
        public MatrixPosition Position { get; }
        public double Value { get; }

        public int Row => Position.Row;
        public int Col => Position.Col;

        public MatrixEntry(int row, int col, double value)
            : this(new MatrixPosition(row, col), value)
        {
        }

        public MatrixEntry(MatrixPosition position, double value)
        {
            Position = position;
            Value = value;
        }
    }

    /// <summary>
    /// Накопитель вкладов для сборки <see cref="CSRMatrix"/> (аналог
    /// K[i,j] += local[i,j] при сборке МКЭ). Повторные вклады в одну и ту же
    /// позицию суммируются. Когда сборка завершена, <see cref="Build"/>
    /// строит готовую неизменяемую матрицу.
    /// </summary>
    public class CSRMatrixBuilder
    {
        private readonly Dictionary<MatrixPosition, double> buffer = new Dictionary<MatrixPosition, double>();

        private readonly int rows;
        private readonly int cols;

        public CSRMatrixBuilder(int rows, int cols)
        {
            this.rows = rows;
            this.cols = cols;
        }

        public void AddToElement(int row, int col, double value)
        {
            if (row < 0 || row >= rows || col < 0 || col >= cols)
                throw new IndexOutOfRangeException($"Indices are out of range: ({row}, {col}).");

            var position = new MatrixPosition(row, col);
            buffer.TryGetValue(position, out double existing);
            buffer[position] = existing + value;
        }

        /// <summary>
        /// Строит готовую матрицу из накопленных вкладов.
        /// </summary>
        public CSRMatrix Build()
        {
            var elements = buffer.Select(kv => new MatrixEntry(kv.Key, kv.Value));
            return new CSRMatrix(rows, cols, elements);
        }
    }
}
