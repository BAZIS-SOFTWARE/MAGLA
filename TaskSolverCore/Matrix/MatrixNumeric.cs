using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Matrix
{
    public enum MatrixKind
    {
        profile,
        band
    }
    public enum MatrixStorage
    {
        simmetry,
        nonsimmetry
    }



    public abstract class MatrixNumeric<T> where T : INumber<T>
    {
        internal T[][] values;
        internal List<int>[] r_inds;
        /// <summary>
        /// Индексы строк
        /// </summary>
        public List<int>[] R_Inds
        {
            get { return r_inds; }
        }
        /// <summary>
        /// Индексы колонн
        /// </summary>
        internal List<int>[] c_inds;

        public List<int>[] C_Inds
        {
            get { return c_inds; }
        }

        public T[][] Values
        {
            get
            {
                return values;
            }
        }

        /// <summary>
        /// SetIncidents
        /// </summary>
        /// <param name="inds"></param>
        public abstract void SetIncidents(List<int>[] inds);

        public int Length { get { return values.Length; } }
        /// <summary>
        /// Storage
        /// </summary>
        public MatrixStorage Storage { get; internal set; } = MatrixStorage.simmetry;
        /// <summary>
        /// Kind
        /// </summary>
        public abstract MatrixKind Kind { get; }
        /// <summary>
        /// Доступ к элементу
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public virtual T this[int row, int col]
        {
            get { return values[row][col]; }
            set
            {
                values[row][col] = value;
            }
        }

        public T[] this[int row]
        {
            get { return values[row]; }
            set { values[row] = value; }
        }

        public abstract void LineCross(T[] vector, T multValue, int index);

        public List<int>[] TransposeIndexes()
        {
            //var simbolMatrix = new int[matrix.GetLength(0)][]; // pointer to global nonzero upper diag indexes
            var tranIndex = new List<int>[values.Length];

            for (int i = 0; i < Length; i++)
            {
                tranIndex[i] = new List<int>();
            }


            for (int i = 0; i < Length; i++)
            {
                for (int j = 0; j < r_inds[i].Count; j++)
                {
                    var col = r_inds[i][j];
                    tranIndex[col].Add(i);
                }
            }

            return tranIndex;
        }

        //public abstract MatrixNumeric<T> ToCSCFormat();

        //public abstract MatrixNumeric<T> ToCSRFormat();

        public List<IndexItem>[] GetSplitIndexes(int blockSize)
        {
            //var simbolMatrix = new int[matrix.GetLength(0)][]; // pointer to global nonzero upper diag indexes
            var splitIndex = new List<IndexItem>[values.Length];

            for (int i = 0; i < Length; i++)
            {
                var size = (int)Math.Sqrt(r_inds[i].Count);
                var counter = 1;

                var index = r_inds[i][0];
                var item = new IndexItem()
                { Value = index, Start = 0, Stop = 0 };
                var list = new List<IndexItem>() { item };

                if (size > blockSize)
                {
                    for (int j = 1; j < r_inds[i].Count; j++)
                    {
                        if (counter == size)
                        {
                            index = r_inds[i][j];
                            item = new IndexItem()
                            { Value = index, Start = j, Stop = 0 };
                            list.Add(item);
                            counter = 0;
                        }
                        list.Last().Stop = j;
                        counter++;
                    }
                }
                else
                {
                    list.Last().Stop = r_inds[i].Count - 1;
                }
                splitIndex[i] = list;
            }

            return splitIndex;
        }

        public abstract void Divide(T divisor);

        public abstract T[] MultVector(T[] vector);

        //public abstract MatrixNumeric<T> MultMatrix(MatrixNumeric<T> matrix);

        public void Clear()
        {
            for (int i = 0; i < Length; i++)
                Array.Clear(values[i], 0, values[i].Length);
        }

        //public abstract void ReduceZeroItems();
        public abstract void ReduceZeroIndexes();
        public abstract void ReduceZeroElements();
    }
}
