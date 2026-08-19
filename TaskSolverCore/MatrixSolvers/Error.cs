using PropertiesCalculator.PropertiesCalculator.MetallurgicalModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.MatrixSolvers
{
    public static class Error
    {
        public static double AbsoluteMax(double[] x1, double[] x2)
        {
            var length = x1.Length;
            var dx = new double[length];

            for (int i = 0; i < length; i++)
            {
                var resu = Math.Abs(x2[i] - x1[i]);

                if (double.IsNaN(resu) | double.IsInfinity(resu))
                    return -1;
                dx[i] = resu;
            }

            return dx.Max();
        }
    }
}
