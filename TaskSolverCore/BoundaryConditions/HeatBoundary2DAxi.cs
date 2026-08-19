using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using TaskSolverCore.Extensions;

namespace TaskSolverCore.BoundaryConditions
{
    public class HeatBoundary2DAxi : IHeatBoundary
    {
        private double CalcInterpolatedValue(IEnumerable<float> values, double[] N)
        {
            var counter = 0;
            var intpValue = 0.0;
            foreach (var value in values)
                intpValue += value * N[counter++];
            return intpValue;
        }
   
/// <inheritdoc/>

        public Vector<double> FlowBoundary_Calc(IElement elem, double mediaTemp, double heatExch)
        {
            var sumFlowBoundary = Vector<double>.Build.Dense(elem.NumberOfPoints);
            var x_Coords = elem.GetVertexes().Select(x => x.Position._x);
            var y_Coords = elem.GetVertexes().Select(x => x.Position._y);

            foreach (var intPoint in elem.GetIntegralPoints())
            {
                //var vFlowBoundary = Vector<double>.Build.Dense(elem.NumberOfPoints);
                var dN = elem.GetDerivativesFormFunctions(intPoint);

                var n = elem.GetFormFunctions(intPoint);

                var vN = Vector<double>.Build.DenseOfArray(n);

                var dr = CalcInterpolatedValue(x_Coords, dN.GetRow(0));
                var dz = CalcInterpolatedValue(y_Coords, dN.GetRow(0));

                var length = Math.Sqrt(dr * dr + dz * dz) * intPoint.Weigt;

                var r = CalcInterpolatedValue(x_Coords, n); //вычислим растояние до каждой точки интегрирования в цикле

                var sqr = 2 * 3.14f * r * length;

                vN.Multiply(heatExch * mediaTemp * sqr, vN);
                sumFlowBoundary = sumFlowBoundary.Add(vN);

            }
            return sumFlowBoundary;
        }

        public Vector<double> FlowHeat_Calc(IElement elem, float flowValue)
        {
            var sumFlowBoundary = Vector<double>.Build.Dense(elem.NumberOfPoints);
            var x_Coords = elem.GetVertexes().Select(x => x.Position._x);
            var y_Coords = elem.GetVertexes().Select(x => x.Position._y);

            foreach (var intPoint in elem.GetIntegralPoints())
            {
                //var vFlowBoundary = Vector<double>.Build.Dense(elem.NumberOfPoints);
                var dN = elem.GetDerivativesFormFunctions(intPoint);

                var n = elem.GetFormFunctions(intPoint);

                var vN = Vector<double>.Build.DenseOfArray(n);

                var dr = CalcInterpolatedValue(x_Coords, dN.GetRow(0));
                var dz = CalcInterpolatedValue(y_Coords, dN.GetRow(0));

                var length = Math.Sqrt(dr * dr + dz * dz) * intPoint.Weigt;

                var r = CalcInterpolatedValue(x_Coords, n); //вычислим растояние до каждой точки интегрирования в цикле

                var sqr = 2 * 3.14f * r * length;

                vN.Multiply(flowValue * sqr, vN);
                sumFlowBoundary = sumFlowBoundary.Add(vN);

            }
            return sumFlowBoundary;
        }

        //public Vector<double> VolumeHeat_Calc(ElementItem elementItem, double heatValue)
        //{
        //    throw new NotImplementedException();
        //}
/// <inheritdoc/>

        public Matrix<double> ExchangeBoundary_Calc(IElement elem, double heatExch)
        {
            var sumHeatExch = Matrix<double>.Build.Dense(elem.NumberOfPoints, elem.NumberOfPoints);
            var x_Coords = elem.GetVertexes().Select(x => x.Position._x);
            var y_Coords = elem.GetVertexes().Select(x => x.Position._y);

            foreach (var intPoint in elem.GetIntegralPoints())
            {
                var mHeatExch = Matrix<double>.Build.Dense(elem.NumberOfPoints, elem.NumberOfPoints);
                var dN = elem.GetDerivativesFormFunctions(intPoint); 

                var n = elem.GetFormFunctions(intPoint);

                var mN = Matrix<double>.Build.DenseOfRowArrays(n);
                var mNt = mN.Transpose();

                var dr = CalcInterpolatedValue(x_Coords, dN.GetRow(0));
                var dz = CalcInterpolatedValue(y_Coords, dN.GetRow(0));

                var length = Math.Sqrt(dr * dr + dz * dz) * intPoint.Weigt;

                var r = CalcInterpolatedValue(x_Coords, n); //вычислим растояние до каждой точки интегрирования в цикле

                var sqr = 2 * 3.14f * r * length;

                mNt.Multiply(mN, mHeatExch);

                mHeatExch.Multiply(heatExch * sqr, mHeatExch);
                sumHeatExch = sumHeatExch.Add(mHeatExch);

            }
            return sumHeatExch;
        }
    }
}
