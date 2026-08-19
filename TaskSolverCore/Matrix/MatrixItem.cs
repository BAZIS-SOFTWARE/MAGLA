using System;

namespace TaskSolverCore.Matrix
{
    public class MatrixItem<T>: IComparable<MatrixItem<T>>
    {
        public int Index { get; set; }
        public T Value { get; set; }

        public int CompareTo(MatrixItem<T> other)
        {
            if (Index < other.Index)
                return -1;
            else if (Index > other.Index)
                return 1;
            else return 0;
        }

        public override string ToString()
        {
            return Index.ToString() + " " + Value.ToString();
        }
    }
}
