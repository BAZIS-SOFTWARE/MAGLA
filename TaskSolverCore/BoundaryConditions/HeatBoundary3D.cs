
using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using TaskSolverCore.ElementData;
using TaskSolverCore.Extensions;

namespace TaskSolverCore.BoundaryConditions
{
    public class HeatBoundary3D : IHeatBoundary
    {   
/// <inheritdoc/>

        public Vector<double> FlowBoundary_Calc(IElement elem, double mediaTemp, double heatExch)
        {
            var sumFlowBoundary = Vector<double>.Build.Dense(elem.NumberOfPoints);
            var x_Coords = elem.GetVertexes().Select(x => x.Position._x);
            var y_Coords = elem.GetVertexes().Select(x => x.Position._y);
            var z_Coords = elem.GetVertexes().Select(x => x.Position._z);

            foreach (var intPoint in elem.GetIntegralPoints())
            {
                //var vFlowBoundary = Vector<double>.Build.Dense(elem.NumberOfPoints);
                var dN = elem.GetDerivativesFormFunctions(intPoint);

                var n = elem.GetFormFunctions(intPoint);

                var vN = Vector<double>.Build.DenseOfArray(n);

                var dx_ksi = CalcInterpolatedValue(x_Coords, dN.GetRow(0)); // ksi
                var dy_ksi = CalcInterpolatedValue(y_Coords, dN.GetRow(0)); // ksi
                var dz_ksi = CalcInterpolatedValue(z_Coords, dN.GetRow(0)); // ksi

                var dx_eta = CalcInterpolatedValue(x_Coords, dN.GetRow(1)); // eta
                var dy_eta = CalcInterpolatedValue(y_Coords, dN.GetRow(1)); // eta
                var dz_eta = CalcInterpolatedValue(z_Coords, dN.GetRow(1)); // eta

                var e = dx_ksi * dx_ksi + dy_ksi * dy_ksi + dz_ksi * dz_ksi;
                var g = dx_eta * dx_eta + dy_eta * dy_eta + dz_eta * dz_eta;
                var f = dx_ksi * dx_eta + dy_ksi * dy_eta + dz_ksi * dz_eta;

                var sqr = Math.Sqrt(e * g - f * f) * intPoint.Weigt;

                vN.Multiply(heatExch * mediaTemp * sqr, vN);
                sumFlowBoundary = sumFlowBoundary.Add(vN);

            }
            return sumFlowBoundary;
        }

        public Vector<double> FlowHeat_Calc(IElement element, float flowValue)
        {
            return FlowHeat_Calc(element, (_, _, _) => flowValue);
        }

        public Vector<double> FlowHeat_Calc(IElement element, Func<double, double, double, double> flowValue)
        {
            var result = Vector<double>.Build.Dense(element.NumberOfPoints);
            var x = element.GetVertexes().Select(node => node.Position._x).ToArray();
            var y = element.GetVertexes().Select(node => node.Position._y).ToArray();
            var z = element.GetVertexes().Select(node => node.Position._z).ToArray();
            foreach (var intPoint in element.GetIntegralPoints())
            {
                var derivatives = element.GetDerivativesFormFunctions(intPoint);
                var functions = element.GetFormFunctions(intPoint);
                var dxKsi = CalcInterpolatedValue(x, derivatives.GetRow(0));
                var dyKsi = CalcInterpolatedValue(y, derivatives.GetRow(0));
                var dzKsi = CalcInterpolatedValue(z, derivatives.GetRow(0));
                var dxEta = CalcInterpolatedValue(x, derivatives.GetRow(1));
                var dyEta = CalcInterpolatedValue(y, derivatives.GetRow(1));
                var dzEta = CalcInterpolatedValue(z, derivatives.GetRow(1));
                var e = dxKsi * dxKsi + dyKsi * dyKsi + dzKsi * dzKsi;
                var g = dxEta * dxEta + dyEta * dyEta + dzEta * dzEta;
                var f = dxKsi * dxEta + dyKsi * dyEta + dzKsi * dzEta;
                var area = Math.Sqrt(Math.Max(0.0, e * g - f * f)) * intPoint.Weigt;
                var px = CalcInterpolatedValue(x, functions);
                var py = CalcInterpolatedValue(y, functions);
                var pz = CalcInterpolatedValue(z, functions);
                result += Vector<double>.Build.DenseOfArray(functions) * (flowValue(px, py, pz) * area);
            }
            return result;
        }

/// <inheritdoc/>

        public Matrix<double> ExchangeBoundary_Calc(IElement elem, double heatExch)
        {
            //var elem = elementItem.Element;

            var sumHeatExch = Matrix<double>.Build.Dense(elem.NumberOfPoints, elem.NumberOfPoints);
            var x_Coords = elem.GetVertexes().Select(x => x.Position._x);
            var y_Coords = elem.GetVertexes().Select(x => x.Position._y);
            var z_Coords = elem.GetVertexes().Select(x => x.Position._z);

            foreach (var intPoint in elem.GetIntegralPoints())
            {
                var mHeatExch = Matrix<double>.Build.Dense(elem.NumberOfPoints, elem.NumberOfPoints);
                var dN = elem.GetDerivativesFormFunctions(intPoint);

                var n = elem.GetFormFunctions(intPoint);

                var mN = Matrix<double>.Build.DenseOfRowArrays(n);
                var mNt = mN.Transpose();

                var dx_ksi = CalcInterpolatedValue(x_Coords, dN.GetRow(0)); // ksi
                var dy_ksi = CalcInterpolatedValue(y_Coords, dN.GetRow(0)); // ksi
                var dz_ksi = CalcInterpolatedValue(z_Coords, dN.GetRow(0)); // ksi

                var dx_eta = CalcInterpolatedValue(x_Coords, dN.GetRow(1)); // eta
                var dy_eta = CalcInterpolatedValue(y_Coords, dN.GetRow(1)); // eta
                var dz_eta = CalcInterpolatedValue(z_Coords, dN.GetRow(1)); // eta

                var e = dx_ksi * dx_ksi + dy_ksi * dy_ksi + dz_ksi * dz_ksi;
                var g = dx_eta * dx_eta + dy_eta * dy_eta + dz_eta * dz_eta;
                var f = dx_ksi * dx_eta + dy_ksi * dy_eta + dz_ksi * dz_eta;

                var sqr = Math.Sqrt(e * g - f * f) * intPoint.Weigt;

                mNt.Multiply(mN, mHeatExch);

                mHeatExch.Multiply(heatExch * sqr, mHeatExch);
                sumHeatExch = sumHeatExch.Add(mHeatExch);

            }
            return sumHeatExch;
        }

        private double CalcInterpolatedValue(IEnumerable<float> values, double[] N)
        {
            var counter = 0;
            var intpValue = 0.0;
            foreach (var value in values)
                intpValue += value * N[counter++];
            return intpValue;
        }
    }
}
