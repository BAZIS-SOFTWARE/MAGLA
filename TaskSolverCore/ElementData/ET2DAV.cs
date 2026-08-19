using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.ElementData
{
    public class ET2DAV : ElementTermal
    {
        public ET2DAV(IElement2D element) : base(element)
        {
        }

        public override Matrix<double> Capacity_Calc()
        {
            var sumCapacity = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);
            var x_Coords = Element.GetVertexes().Select(x => x.Position._x);

            //var rm = x_Coords.Sum() / x_Coords.Count();

            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var mCapacity = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);

                var sqr = Math.Abs(intPoint.Jacobi.Determinant()) * intPoint.Weigt;

                var n = Element.GetFormFunctions(intPoint);
                var r = CalcInterpolatedValue(x_Coords, n); //вычислим растояние до каждой точки интегрирования в цикле

                var vol = 2 * 3.14f * sqr * r;
                var c = HeatCapacity * Density;

                var mN = Matrix<double>.Build.DenseOfRowArrays(n);
                var mNt = mN.Transpose();
                mNt.Multiply(mN, mCapacity);
                mCapacity.Multiply(r, mCapacity);

                mCapacity.Multiply(c * vol, mCapacity);
                sumCapacity = sumCapacity.Add(mCapacity);
            }
            return sumCapacity;
        }

        public override Matrix<double> HeatTransfer_Calc()
        {
            var sumHeatTransf = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);
            var x_Coords = Element.GetVertexes().Select(x => x.Position._x);

            //var rm = x_Coords.Sum() / x_Coords.Count();

            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var mHeatTransf = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);
                var mDN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                var j = intPoint.Jacobi;

                var N = Element.GetFormFunctions(intPoint);
                var r = CalcInterpolatedValue(x_Coords, N); //вычислим растояние до каждой точки интегрирования в цикле

                mDN = j.Inverse().Multiply(mDN);

                var mB = GetFormGradientMatrix(mDN);
                var sqr = Math.Abs(j.Determinant()) * intPoint.Weigt;

                var vol = 2 * 3.14f * r * sqr;

                var propAr = new double[2, 2];

                propAr[0, 0] = r * HeatTransfer[0]; propAr[0, 1] = 0;
                propAr[1, 0] = 0; propAr[1, 1] = r * HeatTransfer[1];

                var mD = Matrix<double>.Build.DenseOfArray(propAr);

                var mBt = mB.Transpose();
                mHeatTransf = mBt.Multiply(mD).Multiply(mB);
                mHeatTransf.Multiply(vol, mHeatTransf);

                sumHeatTransf = sumHeatTransf.Add(mHeatTransf);

            }
            return sumHeatTransf;
        }

        private double CalcInterpolatedValue(IEnumerable<float> values, double[] N)
        {
            var counter = 0;
            var intpValue = 0.0;
            foreach (var value in values)
                intpValue += value * N[counter++];
            return intpValue;
        }

        public Matrix<double> GetFormGradientMatrix(Matrix<double> dN)
        {
            var numberOfPoints = dN.ColumnCount;
            var B = Matrix<double>.Build.Dense(2, numberOfPoints);
            for (int m = 0; m < numberOfPoints; m++)
            {
                B[0, m] = dN[0, m];
                B[1, m] = dN[1, m];
            }
            return B;
        }

        public override Vector<double> VolumeHeat_Calc(double heatValue)
        {
            throw new NotImplementedException();
        }
    }
}
