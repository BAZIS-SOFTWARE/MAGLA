using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.MatrixSolvers
{
    public class ConjugateGradients : MatrixSolver
    {
        public ConjugateGradients(int processors) : this()
        {
            ParallelOptions.MaxDegreeOfParallelism = processors;
        }

        public ConjugateGradients()
        {

        }

        public Tuple<double[], int, double> Solve(double[] y, double[][] m, List<int>[] ui, List<int>[] li, float eps,int iterMax)
        {
            var x = new List<double[]>();
            var r = new List<double[]>();
            var z = new List<double[]>();

            List<double>[] mu, ml;

            MatricesCreation(m, ui, li, out mu, out ml);

            x.Add(new double[y.Length]); // x.Add(y);
            r.Add(y);
            z.Add(r.Last());

            var iterCounter = 0;
            var max = 0.0;
            //var iterationThreshold = 10;
            while (true)
            {
                var zl = z[iterCounter];
                var rl = r[iterCounter];
                
                double[] mz;

                if (!SMP)
                    mz = MultiplyMatrixToVector(mu, ml, ui, li, zl);
                else
                    mz = MultiplyMatrixToVectorSMP(mu, ml, ui, li, zl);

                var mzzl = MultiplyVectorToVector(mz, zl);
                var rlrl = MultiplyVectorToVector(rl, rl);
                var a = rlrl / mzzl;
                if (double.IsNaN(a) | double.IsInfinity(a))
                    throw new Exception("Coefficient a is NaN or infinite.");

                var za = MultiplyVectorToValue(zl, a);
                var xi = SumVectorToVector(za, x[iterCounter]);
                
                x.Add(xi);
                
                //var maxX = ri.Max(i => Math.Abs(i));
               //var maxY = y.Max(i => Math.Abs(i));

                var dx = GetAbsoluteError(x[iterCounter], x[iterCounter + 1]);
                max = dx.Max();
               // max = maxX / maxY;
                if (max < eps)
                    break;

                iterCounter++;

                if (iterCounter >= iterMax)
                    break;

                var mza = MultiplyVectorToValue(mz, a);

                var dr = SubVectorToVector(mza, rl);
                r.Add(dr);
                var drdr = MultiplyVectorToVector(dr, dr);
                var b = drdr / rlrl;
                if (double.IsNaN(b) | double.IsInfinity(b))
                    throw new Exception("Coefficient b is NaN or infinite.");

                var zb = MultiplyVectorToValue(zl, b);
                var zi = SumVectorToVector(dr, zb);
                z.Add(zi);
            }
            return new Tuple<double[], int, double>(x.Last(), iterCounter, max);
        }

        private double[] GetAbsoluteError(double[] x1, double[] x2)
        {
            var length = x1.Length;
            var dx = new double[length];

            for (int i = 0; i < length; i++)
            {
                var resu = Math.Abs(x2[i] - x1[i]);

                if (double.IsNaN(resu) | double.IsInfinity(resu))
                    throw new Exception("The value is NaN or infinite.");
                dx[i] = resu;
            }

            return dx;
        }

        private double[] SubVectorToVector(double[] v1, double[] v2)
        {
            var length = v1.Length;

            var vi = new double[length];

            for (int i = 0; i < length; i++)
                vi[i] = v2[i] - v1[i];

            return vi;
        }

        private double[] SumVectorToVector(double[] v1, double[] v2)
        {
            var length = v1.Length;

            var vi = new double[length];

            for (int i = 0; i < length; i++)
                vi[i] = v1[i] + v2[i];

            return vi;
        }

        private double[] MultiplyVectorToValue(double[] v, double val)
        {
            var length = v.Length;

            var vi = new double[length];

            for (int i = 0; i < length; i++)
                vi[i] = v[i] * val;

            return vi;
        }

        private double MultiplyVectorToVector(double[] v1, double[] v2)
        {
            var prod = 0.0;

            for (int i = 0; i < v1.Length; i++)
            {
                prod = prod + v1[i] *v2[i];
            }
            return prod;
        }

        private double[] MultiplyMatrixToVector(List<double>[] mu, List<double>[] ml, List<int>[] ui, List<int>[] li, double[] vec)
        {
            var length = vec.Length;

            var res = new double[length];

            for (int i = 0; i < length; i++)
            {
                var sum = 0.0;
                var rowLen = ui[i].Count;
                for (int j = 0; j < rowLen; j++)
                {
                    var row = ui[i][j];
                    sum = sum + mu[i][j] * vec[row];
                }

                rowLen = li[i].Count - 1;
                for (int j = 0; j < rowLen; j++)
                {

                    var row = li[i][j];
                    sum = sum + ml[i][j] * vec[row];
                }

                res[i] = sum;
            }
            return res;
        }

        private double[] MultiplyMatrixToVectorSMP(List<double>[] mu, List<double>[] ml, List<int>[] ui, List<int>[] li, double[] vec)
        {
            var length = vec.Length;

            var res = new double[length];

            Parallel.For(0, length, i =>
            {
                var sum = 0.0;
                var rowLen = ui[i].Count;
                for (int j = 0; j < rowLen; j++)
                {
                    var row = ui[i][j];
                    sum = sum + mu[i][j] * vec[row];
                }

                rowLen = li[i].Count - 1;
                for (int j = 0; j < rowLen; j++)
                {

                    var row = li[i][j];
                    sum = sum + ml[i][j] * vec[row];
                }

                res[i] = sum;
            });
            return res;
        }


        private void MatricesCreation(double[][] m, List<int>[] ui, List<int>[] li, out List<double>[] mu, out List<double>[] ml)
        {
            mu = new List<double>[m.Length];
            ml = new List<double>[m.Length];

            for (int i = 0; i < m.Length; i++)
            {
                mu[i] = new List<double>() { m[i][0] };
                ml[i] = new List<double>();

                for (int j = 1; j < m[i].Length; j++)
                {
                    mu[i].Add(m[i][j]);
                }

                var rowLen = li[i].Count - 1;
                for (int j = 0; j < rowLen; j++)
                {
                    var row = li[i][j];
                    var col = ui[row].BinarySearch(i);
                    ml[i].Add(m[row][col]);
                }
                ml[i].Add(m[i][0]);
            }
        }
    }
}
