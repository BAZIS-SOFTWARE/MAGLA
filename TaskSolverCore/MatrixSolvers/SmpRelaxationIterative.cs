using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.MatrixSolver
{
    public class SmpRelaxationIterative
    {
        /// <summary>
        /// ParDegree
        /// </summary>
        public int ParDegree { get; }
        public SmpRelaxationIterative(int parDegree)
        {
            ParDegree = parDegree;
        }
        public Tuple<double[], int, double, float> Solve(double[] y, double[][] m, List<int>[] ui, List<int>[] li, float eps, float w, float wm, int iterMax)
        {
            var x = new List<double[]>();
            List<double>[] bu, bl;

            MatricesCreation(y, m, ui, li, out bu, out bl);

            x.Add(y); // x.Add(y);

            var iterCounter = 0;
            var max = 0.0;
            //var iterationThreshold = 10;
            while (true)
            {
                var xi = MultiplyMatrixToVector(y, bu, bl, ui, li, x.Last(), w);

                x.Add(xi);

                var error = Error.Absolute(x[iterCounter], x[iterCounter + 1]);
                max = error;

                if (max == -1)
                    break;

                if (max < eps)
                    break;

                iterCounter++;

                if (iterCounter >= iterMax)
                {
                    w = w + 0.05f;
                    iterCounter = 0;
                    x.Clear();
                    x.Add(y); // x.Add(y);
                    if (w > wm) break;
                }

            }
            return new Tuple<double[], int, double, float>(x.Last(), iterCounter, max, w);
        }

        private void MatricesCreation(double[] y, double[][] m, List<int>[] ui, List<int>[] li, out List<double>[] bu, out List<double>[] bl)
        {
            bu = new List<double>[m.Length];
            bl = new List<double>[m.Length];

            for (int i = 0; i < m.Length; i++)
            {
                bu[i] = new List<double>() { 0 };
                bl[i] = new List<double>();

                for (int j = 1; j < m[i].Length; j++)
                {
                    bu[i].Add(-m[i][j] / m[i][0]);
                }
                y[i] = y[i] / m[i][0];

                var rowLen = li[i].Count - 1;
                for (int j = 0; j < rowLen; j++)
                {
                    var row = li[i][j];
                    var col = ui[row].BinarySearch(i);
                    bl[i].Add(-m[row][col] / m[i][0]);
                }
                bl[i].Add(0);
            }
        }

        private double[] MultiplyMatrixToVector(double[] y, List<double>[] bu, List<double>[] bl, List<int>[] ui, List<int>[] li, double[] x0, float w)
        {
            var length = y.Length;

            var xi = new double[length];

            for (int i = 0; i < length; i++)
                xi[i] = x0[i];

            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = ParDegree
            };


            Parallel.For(0, length,i =>
            {
                var sum = 0.0;
                var rowLen = ui[i].Count;
                for (int j = 0; j < rowLen; j++)
                {
                    var row = ui[i][j];
                    sum = sum + bu[i][j] * xi[row];
                }

                rowLen = li[i].Count - 1;
                for (int j = 0; j < rowLen; j++)
                {

                    var row = li[i][j];
                    sum = sum + bl[i][j] * xi[row];
                }

                xi[i] = w * (sum + y[i]) + (1 - w) * x0[i];
            });

            return xi;
        }
    }
}
