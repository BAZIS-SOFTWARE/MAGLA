using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace TaskSolverCore.Matrix
{
    public class BandMatrix<T> : MatrixNumeric<T> where T : INumber<T>
    {

        public int Width { get; }
/// <inheritdoc/>

        public override MatrixKind Kind => MatrixKind.band;

        public override T this[int row, int col]
        {
            get
            {
                //var scol = R_Inds[row].BinarySearch(col);
                var scol = col - row;

                if (scol >= Width)
                    throw new Exception($"Indices are outside the bandwidth: {row}, {col}.");

                return values[row][scol];
            }
            set
            {
                //if (col < row)
                //throw new Exception("В симметричной матрице индекс ряда не может быть больше индекса колонны");
                //var scol = R_Inds[row].BinarySearch(col);
                var scol = col - row;

                if (scol >= Width)
                    throw new Exception($"Indices are outside the bandwidth: {row}, {col}.");
                
                values[row][scol] = value;
            }
        }

        public BandMatrix(int length, int band)
        {
            Width = band;

            values = new T[length][];
            r_inds = new List<int>[length];

            var shrink = length - band;

            for (int i = 0; i < length; i++)
            {
                values[i] = new T[band];
                /*
                if (i < shrink)
                    values[i] = new T[band];
                else
                    values[i] = new T[length - i];
                */
            }    

        }

        public BandMatrix(T[][] array, int width)
        {
            Width = width;

            values = new T[array.Length][];
            r_inds = new List<int>[array.Length];
            for (int i = 0; i < Length; i++)
            {
                values[i] = new T[width];
                r_inds[i] = new List<int>();
                for (int j = 0; j < Width; j++)
                {
                    if (!array[i][j].Equals(T.Zero))
                    {
                        r_inds[i].Add(i + j);
                    }
                    values[i][j] = array[i][j];
                }
            }

            c_inds = TransposeIndexes();
                
        }

        public override string ToString()
        {
            var str = "";
            for (int i = 0; i < values.Length; i++)
            {
                var strAr = new string[values[i].Length];
                strAr[0] = values[i][0].ToString();

                for (int j = 1; j < values[i].Length; j++)
                {                   
                    strAr[j] = values[i][j].ToString();
                }
                str = str + string.Join(" ", strAr) + "\n";
            }
            return str;
        }


        public void SetIndexes(List<int>[] indexes)
        {
            for (int i = 0; i < indexes.Length; i++)
            {
                r_inds[i] = indexes[i];
            }
            c_inds = TransposeIndexes();
        }

        public override T[] MultVector(T[] vector)
        {
            var res = new T[vector.Length];

            var trans = TransposeIndexes();
            var transInds = trans;

            for (int i = 0; i < Length; i++)
            {
                var sum = T.Zero;
                var length = r_inds[i].Count;
                for (int j = 0; j < length; j++)
                {
                    var row = r_inds[i][j];
                    sum = sum + values[i][row - i] * vector[row];
                }

                length = transInds[i].Count - 1;
                for (int j = 0; j < length; j++)
                {

                    var row = transInds[i][j];
                    sum = sum + values[row][i - row] * vector[row];
                }

                res[i] = sum;
            }

            return res;

        }

        public override void Divide(T divisor)
        {
            for (int i = 0; i < Length; i++)
            {
                var length = values[i].Length;
                for (int j = 0; j < length; j++)
                {
                    if (!values[i][j].Equals(T.Zero))
                    {
                        var row = r_inds[i][j];
                        var res = values[i][row - i] / divisor;
                        values[i][row - i] = res;
                    }
                }
            }
        }

        public override void LineCross(T[] vector, T multValue, int index)
        {
            vector[index] = values[index][0] * multValue; // обработка диагонального

            var length = r_inds[index].Count;
            var sum = T.Zero;
            for (int j = 1; j < length; j++)
            {
                var row = r_inds[index][j];

                sum = vector[row] - (values[index][row - index] * multValue);
                values[index][row - index] = T.Zero;

                vector[row] = sum;
            }

            length = c_inds[index].Count - 1;
            for (int j = 0; j < length; j++)
            {
                var row = c_inds[index][j];

                sum = vector[row] - values[row][index - row] * multValue;
                values[row][index - row] = T.Zero;

                vector[row] = sum;
            }
        }

        public override void ReduceZeroElements()
        {
            throw new NotImplementedException();
        }

        public override void ReduceZeroIndexes()
        {
            for (int i = 0; i < Length; i++)
            {
                r_inds[i] = new List<int>();
                for (int j = 0; j < values[i].Length; j++)
                {
                    if (!values[i][j].Equals(T.Zero))
                    {
                        r_inds[i].Add(i + j);
                    }
                }
            }
        }

        //public override MatrixNumeric<T> MultMatrix(MatrixNumeric<T> matrix)
        //{
        //    throw new NotImplementedException();
        //}

        public override void SetIncidents(List<int>[] inds)
        {
            r_inds = new List<int>[inds.Length];
            c_inds = new List<int>[inds.Length];

            //values = new T[inds.Length][];

            for (int i = 0; i < Length; i++)
            {

                r_inds[i] = new List<int>();
                c_inds[i] = new List<int>();
                // принудительно сортируем если индексы не по порядку
                inds[i].Sort();

                r_inds[i].Add(i);
                for (int j = 0; j < inds[i].Count; j++)
                {
                    if (inds[i][j] > i)
                        r_inds[i].Add(inds[i][j]);
                    //else if (inds[i][j] < i)
                    //    c_inds[i].Add(inds[i][j]);
                    //else
                    //{
                        //r_inds[i].Add(inds[i][j]);
                        //c_inds[i].Add(inds[i][j]);
                    //}
                }
                // не изменяем
                //values[i] = new T[r_inds[i].Count];
                //c_inds[i].Add(i);
            }

            // временно добавим метод повторно генерирующий индексы колонн!!!
            // TODO изменить вход так, чтобы за один цикл получать все индексы
            c_inds = TransposeIndexes();

        }
        //!!!Вообще этот код должен быть рабочим!!!

        //public override void SetIncidents(List<int>[] inds)
        //{
        //    r_inds = new List<int>[inds.Length];
        //    c_inds = new List<int>[inds.Length];

        //    //values = new T[inds.Length][];

        //    for (int i = 0; i < Length; i++)
        //    {

        //        r_inds[i] = new List<int>();
        //        c_inds[i] = new List<int>();
        //        // принудительно сортируем если индексы не по порядку
        //        inds[i].Sort();

        //        r_inds[i].Add(i);
        //        for (int j = 0; j < inds[i].Count; j++)
        //        {
        //            if (inds[i][j] > i)
        //                r_inds[i].Add(inds[i][j]);
        //            else if (inds[i][j] < i)
        //                c_inds[i].Add(inds[i][j]);
        //            else
        //            {
        //                r_inds[i].Add(inds[i][j]);
        //                c_inds[i].Add(inds[i][j]);
        //            }
        //        }
        //        // не изменяем
        //        //values[i] = new T[r_inds[i].Count];
        //        c_inds[i].Add(i);
        //    }

        //    // временно добавим метод повторно генерирующий индексы колонн!!!
        //    // TODO изменить вход так, чтобы за один цикл получать все индексы
        //    //c_inds = TransposeIndexes();

        //}
    }
}
