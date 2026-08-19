using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.MatrixSolvers
{
    public class LLTDirect : MatrixSolver
    {
        public LLTDirect(int processors) : this()
        {
            ParallelOptions.MaxDegreeOfParallelism = processors;
        }

        public LLTDirect()
        {

        }

        public void L_CholeskyDecomposition_half(double[][] matrix, int bandWidth)
        {
            //double[][] L = new double[matrix.Length][];
            for (int i = 0; i < matrix.Length; i++)
            {
                //L[i] = new double[i + 1]; //L - треугольная матрица, поэтому в i-ой строке i+1 элементов

                double temp;
                //Сначала вычисляем значения элементов слева от диагонального элемента,
                //так как эти значения используются при вычислении диагонального элемента.
                for (int j = 0; j < i; j++)
                {
                    temp = 0.0;
                    for (int k = 0; k < j; k++)
                    {
                        temp += matrix[i][k] * matrix[j][k];
                    }
                    matrix[i][j] = (matrix[i][j] - temp) / matrix[j][j];
                }

                //Находим значение диагонального элемента
                temp = matrix[i][i];
                for (int k = 0; k < i; k++)
                {
                    temp -= matrix[i][k] * matrix[i][k];
                }
                matrix[i][i] = (float)Math.Sqrt(temp);
            }
        }
        public double[] LSolve(double[][] l, double[] b)
        {
            int n = l.GetLength(0) - 1;
            var result = new double[l.GetLength(0)];
            var sumu = 0.0;

            //Solve for x by using forward substitution
            for (int i = 0; i <= n; i++)
            {
                sumu = 0;
                for (int j = 0; j < i; j++)
                {
                    sumu = sumu + (l[i][j] * result[j]);
                }

                result[i] = (b[i] - sumu) / l[i][i];
            }
            return result;
        }
        public double[] LTSolve(double[][] lt, double[] y)
        {
            int n = lt.GetLength(0) - 1;
            var result = new double[lt.GetLength(0)];
            var sumu = 0.0;

            //Solve for x by using back substitution
            for (int i = n; i >= 0; i--)
            {
                sumu = 0;
                for (int j = i + 1; j <= n; j++)
                {
                    sumu = sumu + (lt[j][i] * result[j]);
                }
                result[i] = (y[i] - sumu) / lt[i][i];
            }
            return result;
        }
    }
}
