using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;

namespace TaskSolverCore.ElementData
{
    /// <summary>Тепловой конечный элемент плоской 2D-постановки.</summary>
    public class ET2DPV : ElementTermal
    {
        public ET2DPV(IElement2D element) : base(element)
        {
        }

        public override Matrix<double> Capacity_Calc()
        {
            var result = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);
            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var n = Vector<double>.Build.DenseOfArray(Element.GetFormFunctions(intPoint));
                var area = Math.Abs(intPoint.Jacobi.Determinant()) * intPoint.Weigt;
                result += n.OuterProduct(n) * (Density * HeatCapacity * area);
            }
            return result;
        }

        public override Matrix<double> HeatTransfer_Calc()
        {
            var result = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);
            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var derivatives = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                var jacobi = intPoint.Jacobi;
                derivatives = jacobi.Inverse().Multiply(derivatives);
                var conductivity = Matrix<double>.Build.DenseOfArray(new[,] { { HeatTransfer[0], 0.0 }, { 0.0, HeatTransfer[1] } });
                var area = Math.Abs(jacobi.Determinant()) * intPoint.Weigt;
                result += derivatives.Transpose() * conductivity * derivatives * area;
            }
            return result;
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
                    for (var direction = 0; direction < 2; direction++)
                        directionalGradient[node] += ConvectionVelocity[direction] * derivatives[direction, node];

                var area = Math.Abs(jacobi.Determinant()) * intPoint.Weigt;
                result += n.OuterProduct(directionalGradient) * (Density * HeatCapacity * area);
            }
            return result;
        }

        public override Vector<double> VolumeHeat_Calc(double heatValue)
        {
            var result = Vector<double>.Build.Dense(Element.NumberOfPoints);
            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var n = Vector<double>.Build.DenseOfArray(Element.GetFormFunctions(intPoint));
                var area = Math.Abs(intPoint.Jacobi.Determinant()) * intPoint.Weigt;
                result += n * (heatValue * area);
            }
            return result;
        }
    }
}
