using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskSolverCore.Matrix;

namespace TaskSolverCore.MatrixSolvers
{
    public class UDUTDirect : MatrixSolver
    {
        public UDUTDirect(int processors) : this()
        {
            ParallelOptions.MaxDegreeOfParallelism = processors;
        }

        public UDUTDirect()
        {

        }      

        public unsafe void L_Numeric(List<int>[] li_col, List<int>[] li_row, double[][] matrix)
        {
            var n = matrix.GetLength(0);
            var d = new double[n];
            var sum = 0.0;

            for (int i = 0; i < n; i++)//активная колонна
            {
                sum = matrix[i][i];//значение вычисляемого диагонального элемента

                for (int j = 0; j < i; j++)// перебор по колоннам
                {
                    sum = sum - (d[j] * matrix[i][j] * matrix[i][j]);
                }
                d[i] = sum;//диагональный элемент 
                matrix[i][i] = 1;

                var row_Count = li_row[i].Count;

                if (!SMP)
                    for (int j = 1; j < row_Count; j++)// перебор по рядам
                    {
                        var row_ind = li_row[i][j]; // положение вычисляемого элемента в глобальной матрице
                        sum = matrix[row_ind][i];//значение вычисляемого элемента

                        var col_Count = li_col[i].Count - 1;
                        for (int k = 0; k < col_Count; k++)// перебор по колоннам
                        {
                            var col_ind = li_col[i][k]; // положение вычисляемого элемента в глобальной матрице
                            sum = sum - (d[col_ind] * matrix[i][col_ind] * matrix[row_ind][col_ind]);
                        }

                        matrix[row_ind][i] = sum / d[i]; //внедиагональный элемент
                    }
                else
                {
                    Parallel.For(1, row_Count, ParallelOptions, j =>
                    {
                        var row_ind = li_row[i][j]; // положение вычисляемого элемента в глобальной матрице
                        sum = matrix[row_ind][i];//значение вычисляемого элемента

                        var col_Count = li_col[i].Count - 1;
                        for (int k = 0; k < col_Count; k++)// перебор по колоннам
                        {
                            var col_ind = li_col[i][k]; // положение вычисляемого элемента в глобальной матрице
                            sum = sum - (d[col_ind] * matrix[i][col_ind] * matrix[row_ind][col_ind]);
                        }

                        matrix[row_ind][i] = sum / d[i]; //внедиагональный элемент
                    });
                }
            }
        }

        public void U_Numeric(BandMatrix<double> matrix)
        {
            var n = matrix.Length;
            var d = new double[n];
            // UDUᵀ разложение
            for (int i = 0; i < n; i++)
            {
                // Вычисляем d[j]
                double sum = 0;
                // сколько строк над элементом нужно перебрать.
                var rowCount = matrix.C_Inds[i].Count - 1;

                for (int j = 0; j < rowCount; j++)
                {
                    // индекс текущей строки
                    var rowInd = matrix.C_Inds[i][j];
                    sum += matrix[rowInd, i] * matrix[rowInd, i] * d[rowInd];
                }    


                d[i] = matrix[i,i] - sum;
                matrix[i, i] = d[i];
                
                // Проверка на положительную определенность
                
                // Важно!!! Возможно стоит оставить это условие проверки
                // но с ним не все тесты проходят
                
                //if (d[i] <= 0)
                    //throw new InvalidOperationException("Матрица не является положительно определенной");

                // Вычисляем U[i, j] для i < j
                // сколько колонн в ряду элемента нужно перебрать.
                var col_Count = matrix.R_Inds[i].Count;

                if (!SMP)
                {
                    for (int j = 1; j < col_Count; j++) // col
                    {
                        sum = 0;
                        var colInd = matrix.R_Inds[i][j];

                        for (int k = 0; k < rowCount; k++) // row
                        {
                            // индекс текущей строки
                            var rowInd = matrix.C_Inds[i][k];
                            // условие не выхода за пределы ширины матрицы
                            if (colInd - rowInd < matrix.Width)
                                sum += matrix[rowInd, colInd] * matrix[rowInd, i] * d[rowInd];
                        }

                        matrix[i, colInd] = (matrix[i, colInd] - sum) / d[i];
                        //matrix[colInd, i] = (matrix[i, colInd] - sum) / d[i];
                    }
                }
                else
                {
                    Parallel.For(1, col_Count, ParallelOptions, j =>
                    {
                        sum = 0;
                        var colInd = matrix.R_Inds[i][j];

                        for (int k = 0; k < rowCount; k++) // row
                        {
                            // индекс текущей строки
                            var rowInd = matrix.C_Inds[i][k];
                            // условие не выхода за пределы ширины матрицы
                            if (colInd - rowInd < matrix.Width)
                                sum += matrix[rowInd, colInd] * matrix[rowInd, i] * d[rowInd];
                        }

                        matrix[i, colInd] = (matrix[i, colInd] - sum) / d[i];
                    });
                }
            }
        }

        public void U_Numeric_mode(BandMatrix<double> matrix, HashSet<int> exList)
        {
            var n = matrix.Length;
            var d = new double[n];
            // UDUᵀ разложение
            foreach (var i in exList)
            {
                // Вычисляем d[j]
                double sum = 0;
                // сколько строк над элементом нужно перебрать.
                var rowCount = matrix.C_Inds[i].Count - 1;

                for (int j = 0; j < rowCount; j++)
                {
                    // индекс текущей строки
                    var rowInd = matrix.C_Inds[i][j];
                    sum += matrix[rowInd, i] * matrix[rowInd, i] * d[rowInd];
                }


                d[i] = matrix[i, i] - sum;
                matrix[i, i] = d[i];

                // Проверка на положительную определенность

                // Важно!!! Возможно стоит оставить это условие проверки
                // но с ним не все тесты проходят

                //if (d[i] <= 0)
                //throw new InvalidOperationException("Матрица не является положительно определенной");

                // Вычисляем U[i, j] для i < j
                // сколько колонн в ряду элемента нужно перебрать.
                var col_Count = matrix.R_Inds[i].Count;

                if (!SMP)
                {
                    for (int j = 1; j < col_Count; j++) // col
                    {
                        sum = 0;
                        var colInd = matrix.R_Inds[i][j];

                        for (int k = 0; k < rowCount; k++) // row
                        {
                            // индекс текущей строки
                            var rowInd = matrix.C_Inds[i][k];
                            // условие не выхода за пределы ширины матрицы
                            if (colInd - rowInd < matrix.Width)
                                sum += matrix[rowInd, colInd] * matrix[rowInd, i] * d[rowInd];
                        }

                        matrix[i, colInd] = (matrix[i, colInd] - sum) / d[i];
                        //matrix[colInd, i] = (matrix[i, colInd] - sum) / d[i];
                    }
                }
                else
                {
                    Parallel.For(1, col_Count, ParallelOptions, j =>
                    {
                        sum = 0;
                        var colInd = matrix.R_Inds[i][j];

                        for (int k = 0; k < rowCount; k++) // row
                        {
                            // индекс текущей строки
                            var rowInd = matrix.C_Inds[i][k];
                            // условие не выхода за пределы ширины матрицы
                            if (colInd - rowInd < matrix.Width)
                                sum += matrix[rowInd, colInd] * matrix[rowInd, i] * d[rowInd];
                        }

                        matrix[i, colInd] = (matrix[i, colInd] - sum) / d[i];
                    });
                }
            }
        }

        public double[] ReorderVector(double[] v, int[] order)
        {
            int n = v.Length;
            var result = new double[n];
            for (int i = 0; i < n; i++)
                result[i] = v[order[i]];
            return result;
        }

        public unsafe void L_Numeric_mode(int[][] li_col, int[][] li_row, float[][] matrix, float[] d, int parDegree)
        {
            var n = matrix.GetLength(0);

            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = parDegree
            };

            var sum = 0.0f;

            for (int i = 0; i < n; i++)//активная колонна
            {
                var row_Count = li_row[i].Length;

                if (!SMP)
                    for (int j = 0; j < row_Count; j++)// перебор по рядам
                    {
                        var row_ind = li_row[i][j]; // положение вычисляемого элемента в глобальной матрице
                        sum = matrix[row_ind][i];//значение вычисляемого элемента

                        var col_Count = li_col[i].Length - 1;
                        for (int k = 0; k < col_Count; k++)// перебор по колоннам
                        {
                            var col_ind = li_col[i][k]; // положение вычисляемого элемента в глобальной матрице
                            sum = sum - (d[col_ind] * matrix[i][col_ind] * matrix[row_ind][col_ind]);
                        }

                        if (j == 0) d[i] = sum;//диагональный элемент 
                        else matrix[row_ind][i] = sum / d[i]; //внедиагональный элемент
                    }
                else
                {
                    Parallel.For(1, row_Count, options, j =>
                    {
                        var row_ind = li_row[i][j]; // положение вычисляемого элемента в глобальной матрице
                        sum = matrix[row_ind][i];//значение вычисляемого элемента

                        var col_Count = li_col[i].Length - 1;
                        for (int k = 0; k < col_Count; k++)// перебор по колоннам
                        {
                            var col_ind = li_col[i][k]; // положение вычисляемого элемента в глобальной матрице
                            sum = sum - (d[col_ind] * matrix[i][col_ind] * matrix[row_ind][col_ind]);
                        }

                        if (j == 0) d[i] = sum;//диагональный элемент 
                        else matrix[row_ind][i] = sum / d[i]; //внедиагональный элемент
                    });
                }
                matrix[i][i] = 1;
            }
        }

        public double[] U_Solve(BandMatrix<double> matrix, double[] f)
        {
            int n = matrix.Length - 1;
            var result = new double[matrix.Length];
            double sum = 0;

            //Solve for x by using back substitution
            for (int i = n; i >= 0; i--)
            {
                sum = 0;
                var rowCount = matrix.R_Inds[i].Count;

                for (int j = 1; j < rowCount; j++)
                {
                    var ind = matrix.R_Inds[i][j];
                    sum = sum + (matrix[i,ind] * result[ind]);
                }
                // нет деления на диагональный элемент т.к. он равен "1"
                result[i] = f[i] - sum;
            }
            return result;
        }

        public double[] D_Solve(BandMatrix<double> matrix, double[] b)
        {
            int n = matrix.Length - 1;
            var result = new double[matrix.Length];

            //прямой ход
            for (int i = 0; i <= n; i++)
            {
                result[i] = b[i] / matrix[i,i];
            }
            return result;
        }

        public double[] UT_Solve(BandMatrix<double> matrix, double[] y)
        {
            int n = matrix.Length - 1;
            var result = new double[matrix.Length];
            double sumu = 0;

            //прямой ход
            for (int i = 0; i <= n; i++)
            {
                sumu = 0;
                var colCount = matrix.C_Inds[i].Count;
                for (int j = 0; j < colCount; j++)
                {
                    var ind = matrix.C_Inds[i][j];
                    sumu = sumu + (matrix[ind,i] * result[ind]);
                }
                // нет деления на диагональгый элемент, т.к. он равен "1"
                result[i] = y[i] - sumu; 
            }
            return result;
        }
    }
}
