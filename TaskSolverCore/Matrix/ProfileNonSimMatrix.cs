
using System.Numerics;
using TaskSolverCore.Matrix.Utilities;

namespace TaskSolverCore.Matrix
{
    public class ProfileNonSimMatrix<T> : MatrixNumeric<T> where T : INumber<T>
    {
        /// <inheritdoc/>
        public override MatrixKind Kind => MatrixKind.profile;

        public ProfileNonSimMatrix()
        {
            Storage = MatrixStorage.nonsimmetry;
        }

        public ProfileNonSimMatrix(List<T>[] vals) : this()
        {
            //var inds = new List<int>[vals.Length];
            r_inds = new List<int>[vals.Length];

            values = new T[vals.Length][];
            for (int i = 0; i < vals.Length; i++)
            {
                var temp_v = new List<T>();
                var temp_ui = new List<int>();

                
                for (int j = 0; j < vals[i].Count; j++)
                {
                    if (!vals[i][j].Equals(T.Zero))
                    {
                        temp_v.Add(vals[i][j]);

                        // так как несиммеричная
                        if (j >= i)
                            temp_ui.Add(j);
                    }

                }
                values[i] = temp_v.ToArray();
                r_inds[i] = temp_ui;
            }

            c_inds = TransposeIndexes();
        }

        /// <inheritdoc/>
        public override void LineCross(T[] vector, T multValue, int index)
        {
            vector[index] = multValue; // обработка диагонального

            var length = r_inds[index].Count;

            for (int j = 1; j < length; j++)
            {
                values[index][j] = T.Zero;
            }
            var dInd = c_inds[index].Count - 1; // индекс диагонального элемента
            values[index][dInd] = T.One;
            // вычеркиваем ниже диагонали
            length = c_inds[index].Count - 1;

            for (int j = 0; j < length; j++)
            {
                values[index][j] = T.Zero;
            }
        }


        /// <inheritdoc/>
        public override void SetIncidents(List<int>[] inds)
        {
            r_inds = new List<int>[inds.Length];
            c_inds = new List<int>[inds.Length];

            values = new T[inds.Length][];

            for (int i = 0; i < Length; i++)
            {

                r_inds[i] = new List<int>();
                c_inds[i] = new List<int>();
                // принудительно сортируем если индексы не по порядку
                inds[i].Sort();
                for (int j = 0; j < inds[i].Count; j++)
                {
                    if (inds[i][j] > i)
                        r_inds[i].Add(inds[i][j]);
                    else if (inds[i][j] < i)
                        c_inds[i].Add(inds[i][j]);
                    else
                    {
                        r_inds[i].Add(inds[i][j]);
                        c_inds[i].Add(inds[i][j]);
                    }
                }
                // так как нет симметрии
                values[i] = new T[c_inds[i].Count + r_inds[i].Count - 1];
            }
        }


        public override string ToString()
        {
            var str = "";
            for (int i = 0; i < values.Length; i++)
            {
                var strAr = new string[values[i].Length];
                strAr[0] = Values[i][0].ToString();

                for (int j = 1; j < values[i].Length; j++)
                {
                    strAr[j] = strAr[j] + " " + values[i][j].ToString();
                }
                str = str + string.Join(" ", strAr) + "\n";
            }
            return str;
        }

        public override T[] MultVector(T[] vector)
        {
            throw new NotImplementedException();
        }

        public override void ReduceZeroElements()
        {
            for (int i = 0; i < Length; i++)
            {
                var redValues = new List<T>();
                for (int j = 0; j < values[i].Length; j++)
                {
                    if (!values[i][j].Equals(T.Zero))
                    {
                        redValues.Add(values[i][j]);
                    }
                }
                values[i] = redValues.ToArray();
            }
        }

        public override void ReduceZeroIndexes()
        {
            for (int i = 0; i < Length; i++)
            {
                var temp_r = new List<int>();

                var shift = c_inds[i].Count - 1;
                var l = values[i].Length - shift; //включая диагональный

                for (int j = 0; j < l; j++)
                {
                    if (!values[i][shift + j].Equals(T.Zero))
                    {
                        temp_r.Add(r_inds[i][j]);
                    }
                }
                r_inds[i] = temp_r;
            }

            c_inds = TransposeIndexes();
        }

        public override MatrixNumeric<T> Transpose()
        {
            throw new NotImplementedException();
        }

        public override void Divide(T divisor)
        {
            throw new NotImplementedException();
        }
    }
}
