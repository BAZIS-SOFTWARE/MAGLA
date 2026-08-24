using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using TaskSolverCore.Extensions;

namespace TaskSolverCore.BoundaryConditions
{
    public class HeatBoundary2DPlane : IHeatBoundary
    {
        public Vector<double> FlowBoundary_Calc(IElement element, double mediaTemp, double heatExch) => FlowHeat_Calc(element, (_, _, _) => mediaTemp * heatExch);

        public Vector<double> FlowHeat_Calc(IElement element, float flowValue) => FlowHeat_Calc(element, (_, _, _) => flowValue);

        public Vector<double> FlowHeat_Calc(IElement element, Func<double, double, double, double> flowValue)
        {
            var result = Vector<double>.Build.Dense(element.NumberOfPoints);
            var x = element.GetVertexes().Select(node => node.Position._x).ToArray();
            var y = element.GetVertexes().Select(node => node.Position._y).ToArray();
            foreach (var intPoint in element.GetIntegralPoints())
            {
                var derivatives = element.GetDerivativesFormFunctions(intPoint);
                var functions = element.GetFormFunctions(intPoint);
                var dx = Interpolate(x, derivatives.GetRow(0));
                var dy = Interpolate(y, derivatives.GetRow(0));
                var length = Math.Sqrt(dx * dx + dy * dy) * intPoint.Weigt;
                var px = Interpolate(x, functions);
                var py = Interpolate(y, functions);
                result += Vector<double>.Build.DenseOfArray(functions) * (flowValue(px, py, 0.0) * length);
            }
            return result;
        }

        public Matrix<double> ExchangeBoundary_Calc(IElement element, double heatExch)
        {
            var result = Matrix<double>.Build.Dense(element.NumberOfPoints, element.NumberOfPoints);
            var x = element.GetVertexes().Select(node => node.Position._x).ToArray();
            var y = element.GetVertexes().Select(node => node.Position._y).ToArray();
            foreach (var intPoint in element.GetIntegralPoints())
            {
                var derivatives = element.GetDerivativesFormFunctions(intPoint);
                var functions = Vector<double>.Build.DenseOfArray(element.GetFormFunctions(intPoint));
                var dx = Interpolate(x, derivatives.GetRow(0));
                var dy = Interpolate(y, derivatives.GetRow(0));
                var length = Math.Sqrt(dx * dx + dy * dy) * intPoint.Weigt;
                result += functions.OuterProduct(functions) * (heatExch * length);
            }
            return result;
        }

        private static double Interpolate(IEnumerable<float> values, IReadOnlyList<double> functions)
        {
            var index = 0;
            var result = 0.0;
            foreach (var value in values)
                result += value * functions[index++];
            return result;
        }
    }
}
