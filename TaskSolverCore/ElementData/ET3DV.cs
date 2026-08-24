using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.ElementData
{
    public class ET3DV : ElementTermal
    {
        public ET3DV(IElement3D element) : base(element)
        {
        }

        public override Matrix<double> Capacity_Calc()
        {
            //var elem = elementItem.Element;
            //var capacity = elementItem.HeatCapacity;
            //var density = elementItem.Density;

            var sumCapacity = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);

            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var mCapacity = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);

                // добавление elementItem.GeometryParameter для учета элементов различного типа.
                // по умолчанию равен "1"
                var vol = Math.Abs(intPoint.Jacobi.Determinant()) * intPoint.Weigt;

                var c = HeatCapacity * Density;

                var n = Element.GetFormFunctions(intPoint);
                var mN = Matrix<double>.Build.DenseOfRowArrays(n);
                var mNt = mN.Transpose();
                mNt.Multiply(mN, mCapacity);
                mCapacity.Multiply(vol * c, mCapacity);
                sumCapacity = sumCapacity.Add(mCapacity);
            }
            return sumCapacity;
        }

        public Matrix<double> GetFormGradientMatrix(Matrix<double> dN)
        {
            var numberOfPoints = dN.ColumnCount;
            var emDN = Matrix<double>.Build.Dense(3, numberOfPoints);

            for (int m = 0; m < numberOfPoints; m++)
            {
                emDN[0, m] = dN[0, m];
                emDN[1, m] = dN[1, m];
                emDN[2, m] = dN[2, m];
            }
            return emDN;
        }

        public override Matrix<double> HeatTransfer_Calc()
        {
            //var elem = elementItem.Element;
            //var kk = elementItem.HeatTransfer;

            var sumHeatTransf = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);

            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var mHeatTransf = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);
                var dN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                var j = intPoint.Jacobi;

                dN = j.Inverse().Multiply(dN); // от локальных к глобальным координатам
                var mB = GetFormGradientMatrix(dN);

                // добавление elementItem.GeometryParameter для учета элементов различного типа
                var vol = Math.Abs(j.Determinant()) * intPoint.Weigt;

                var propAr = new double[3, 3];

                propAr[0, 0] = HeatTransfer[0]; propAr[0, 1] = 0; propAr[0, 2] = 0;
                propAr[1, 0] = 0; propAr[1, 1] = HeatTransfer[1]; propAr[1, 2] = 0;
                propAr[2, 0] = 0; propAr[2, 1] = 0; propAr[2, 2] = HeatTransfer[2];

                var mD = Matrix<double>.Build.DenseOfArray(propAr);

                var mBt = mB.Transpose();
                mHeatTransf = mBt.Multiply(mD).Multiply(mB);
                mHeatTransf.Multiply(vol, mHeatTransf);

                sumHeatTransf = sumHeatTransf.Add(mHeatTransf);
            }
            return sumHeatTransf;
        }

        public override Matrix<double> Convection_Calc()
        {
            var result = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);
            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var derivatives = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                var jacobi = intPoint.Jacobi;
                derivatives = jacobi.Inverse().Multiply(derivatives);
                var n = Vector<double>.Build.DenseOfArray(Element.GetFormFunctions(intPoint));
                var directionalGradient = Vector<double>.Build.Dense(Element.NumberOfPoints);
                for (var node = 0; node < Element.NumberOfPoints; node++)
                    for (var direction = 0; direction < 3; direction++)
                        directionalGradient[node] += ConvectionVelocity[direction] * derivatives[direction, node];

                var volume = Math.Abs(jacobi.Determinant()) * intPoint.Weigt;
                result += n.OuterProduct(directionalGradient) * (Density * HeatCapacity * volume);
            }
            return result;
        }

        public override Vector<double> VolumeHeat_Calc(double heatValue)
        {
            var sumVolumeHeat = Vector<double>.Build.Dense(Element.NumberOfPoints);

            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var n = Element.GetFormFunctions(intPoint);
                var vN = Vector<double>.Build.DenseOfArray(n);

                var vol = Math.Abs(intPoint.Jacobi.Determinant()) * intPoint.Weigt;

                vN.Multiply(heatValue * vol, vN);
                sumVolumeHeat = sumVolumeHeat.Add(vN);
            }
            return sumVolumeHeat;
        }
    }
}
