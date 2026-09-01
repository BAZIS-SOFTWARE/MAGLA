using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.MatrixSolvers
{
    public class JacobyIterative : MatrixSolver
    {
        public JacobyIterative(int processors) : this()
        {
            ParallelOptions.MaxDegreeOfParallelism = processors;
        }

        public JacobyIterative()
        {

        }

        public float[] Solve(float[] y, float[][] m, List<int>[] ui, List<int>[] li, float eps)
        {
            var x = new List<float[]>();
            var mMax = 0.0f;

            for (int i = 0; i < m.Length; i++)
            {
                for (int j = 1; j < m[i].Length; j++)
                {
                    m[i][j] = -(m[i][j] / m[i][0]);
                }
                y[i] = y[i] / m[i][0];
                m[i][0] = 0;
                var max = m[i].Max(mi => Math.Abs(mi));
                if (max > mMax)
                    mMax = max;
            }

            x.Add(y);

            while (true)
            {
                var xi = MultiplyMatrixToVector(y, m, ui, li, x.Last());

                x.Add(xi);

                var count = x.Count;
                var dx = new float[m.Length];
                for (int i = 0; i < m.Length; i++)
                {
                    float resu = Math.Abs(x[count - 1][i] - x[count - 2][i]);

                    if (float.IsNaN(resu) | float.IsInfinity(resu))
                        throw new Exception("The value is NaN or infinite.");
                    dx[i] = resu;
                }
                var max = dx.Max();

                if (max < eps)
                    break;
            }
            return x.Last();
        }

        private float[] MultiplyMatrixToVector(float[] y, float[][] m, List<int>[] ui, List<int>[] li, float[] x0)
        {
            var length = m.Length;

            var xi = new float[length];

            for (int i = 0; i < length; i++)
            {
                var sum = 0.0f;
                var rowLen = ui[i].Count;
                for (int j = 0; j < rowLen; j++)
                {
                    var row = ui[i][j];
                    var col = ui[i].BinarySearch(row);
                    sum = sum + m[i][col] * x0[row];
                }

                rowLen = li[i].Count - 1;
                for (int j = 0; j < rowLen; j++)
                {

                    var row = li[i][j];
                    var col = ui[row].BinarySearch(i);
                    sum = sum + m[row][col] * x0[row];
                }

                xi[i] = sum + y[i];
            }
            return xi;
        }
    }
}
