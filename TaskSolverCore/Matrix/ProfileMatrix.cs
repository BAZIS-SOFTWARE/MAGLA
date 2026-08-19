using System.Globalization;
using System.Numerics;
using TaskSolverCore.Matrix.Utilities;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TaskSolverCore.Matrix
{
    public class ProfileMatrix<T> : MatrixNumeric<T> where T : INumber<T>
    {
        /// <summary>
        /// this. Обращение через глобальные индексы (как буд-то она квадратная, а не профильная).
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public override T this[int row, int col]
        {
            get
            {
                //if (col < row)
                    //throw new Exception("В симметричной матрице индекс ряда не может быть больше индекса колонны");

                var scol = R_Inds[row].BinarySearch(col);
                return values[row][scol];
                //if (scol < 0)
                // TODO можно подумать что возвращать если элемент равен нулю.
                // Пока будем возвращать 0.
                //return T.Zero;
                //throw new Exception($"В ряде {row} не найден элемент {col}");         
                //else
                //return values[row][scol];
            }
            set
            {
                //if (col < row)
                    //throw new Exception("В симметричной матрице индекс ряда не может быть больше индекса колонны");

                var scol = R_Inds[row].BinarySearch(col);
                values[row][scol] = value;
            }
        }
        /// <inheritdoc/>
        public override MatrixKind Kind => MatrixKind.profile;

        /// <summary>
        /// Задает индексы строк и колонн ненулевых элементов
        /// </summary>
        /// <param name="inds"></param>
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
                    else if(inds[i][j] < i)
                        c_inds[i].Add(inds[i][j]);
                    else
                    {
                        r_inds[i].Add(inds[i][j]);
                        c_inds[i].Add(inds[i][j]);
                    }
                }
                // так как симметрия
                values[i] = new T[r_inds[i].Count];
            }
        }
        /// <summary>
        /// Пустой конструктор
        /// </summary>
        public ProfileMatrix()
        { }

        /// <summary>
        /// ProfileMatrix. Прямое задание массивов
        /// </summary>
        /// <param name="vals"></param>
        public ProfileMatrix(List<T>[] vals)
        {
            //var inds = new List<int>[vals.Length];
            r_inds = new List<int>[vals.Length];

            values = new T[vals.Length][];
            for (int i = 0; i < vals.Length; i++)
            {
                var temp_v = new List<T>();
                var temp_ui = new List<int>();

                // так как симмеричная
                for (int j = 0; j < vals[i].Count; j++)
                {
                    if (!vals[i][j].Equals(0.0f))
                    {
                        temp_v.Add(vals[i][j]);
                        temp_ui.Add(i + j);
                    }

                }
                values[i] = temp_v.ToArray();
                r_inds[i] = temp_ui;
            }

            c_inds = TransposeIndexes();
        }
        /// <summary>
        /// ProfileMatrix. Прямое задание массивов
        /// </summary>
        /// <param name="type"></param>
        /// <param name="vals"></param>
        public ProfileMatrix(T[][] vals)
        {
            //var inds = new List<int>[vals.Length];
            r_inds = new List<int>[vals.Length];

            values = new T[vals.Length][];
            for (int i = 0; i < vals.Length; i++)
            {
                var temp_v = new List<T>();
                var temp_ui = new List<int>();

                // так как симмеричная
                for (int j = 0; j < vals[i].Length; j++)
                {
                    if (!vals[i][j].Equals(0.0f))
                    {
                        temp_v.Add(vals[i][j]);
                        temp_ui.Add(i + j);
                    }

                }
                values[i] = temp_v.ToArray();
                r_inds[i] = temp_ui;
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
                    var div = r_inds[i][j] - r_inds[i][j - 1];
                    if (div > 1)
                    {
                        strAr[j] = strAr[j] + "0";
                        for (int k = 1; k < div - 1; k++)
                            strAr[j] = strAr[j] + " 0";
                        strAr[j] = strAr[j] + " " + values[i][j].ToString();
                    }

                    else strAr[j] = values[i][j].ToString();
                }
                str = str + string.Join(" ", strAr) + "\n";
            }
            return str;
        }

        public override void LineCross(T[] vector, T multValue, int index)
        {
            vector[index] = values[index][0] * multValue; // обработка диагонального

            var length = r_inds[index].Count;

            var sum = T.Zero;

            for (int j = 1; j < length; j++)
            {
                var row = r_inds[index][j];

                var col = r_inds[index].BinarySearch(row);// возможно можно убрать бинпоиск
                sum = vector[row] - (values[index][col] * multValue);
                values[index][col] = T.Zero;

                vector[row] = sum;
            }

            length = c_inds[index].Count - 1;
            for (int j = 0; j < length; j++)
            {
                var row = c_inds[index][j];

                var col = r_inds[row].BinarySearch(index);
                sum = vector[row] - values[row][col] * multValue;
                values[row][col] = T.Zero;

                vector[row] = sum;
            }
        }

        public override void Divide(T divisor)
        {           
                for (int i = 0; i < Length; i++)
                {
                    var length = values[i].Length;
                    for (int j = 0; j < length; j++)
                    {
                            var res = values[i][j] / divisor;
                            values[i][j] = res;                      
                    }
                }           
        }       

        public override T[] MultVector(T[] vector)
        {
            var res = new T[vector.Length];

            var transInds = TransposeIndexes();

            for (int i = 0; i < Length; i++)
            {
                var sum = T.Zero;
                var length = r_inds[i].Count;
                for (int j = 0; j < length; j++)
                {
                    var row = r_inds[i][j];
                    var col = r_inds[i].BinarySearch(row);
                    sum = sum + values[i][col] * vector[row];
                }

                length = transInds[i].Count - 1;
                for (int j = 0; j < length; j++)
                {
                    var row = transInds[i][j];
                    var col = r_inds[row].BinarySearch(i);
                    sum = sum + values[row][col] * vector[row];
                }

                res[i] = (T)(IConvertible)sum;
            }

            return res;
        }

        public void ReduceZeroItems()
        {
            for (int i = 0; i < Length; i++)
            {
                var tempValues = new List<T>();
                var tempIndexes = new List<int>();
                for (int j = 0; j < values[i].Length; j++)
                {
                    if (!values[i][j].Equals(0.0f))
                    {
                        tempValues.Add(values[i][j]);
                        tempIndexes.Add(r_inds[i][j]);
                    }
                }
                r_inds[i] = tempIndexes;
                values[i] = tempValues.ToArray();
            }
        }

        public override void ReduceZeroIndexes()
        {
            for (int i = 0; i < Length; i++)
            {
                var tempIndexes = new List<int>();
                for (int j = 0; j < values[i].Length; j++)
                {
                    if (!values[i][j].Equals(0.0f))
                    {

                        tempIndexes.Add(r_inds[i][j]);                      
                    }
                }
                r_inds[i] = tempIndexes;
            }

            // здесь имеет смысл сделать обновление колонн
            c_inds = TransposeIndexes();
        }

        public override void ReduceZeroElements()
        {
            for (int i = 0; i < Length; i++)
            {
                var redValues = new List<T>();
                for (int j = 0; j < values[i].Length; j++)
                {
                    if (!values[i][j].Equals(0.0f))
                    {
                        redValues.Add(values[i][j]);
                    }
                }
                values[i] = redValues.ToArray();
            }
        }


    }
}
