namespace CAESolvers
{
    internal sealed class ResidualCalculator
    {
        public double CalculateResidualNorm(
            ICsrMatrix matrix,
            double[] rightHandSide,
            double[] solution)
        {
            var product = matrix.Multiply(solution);
            var residualNormSquared = 0.0;

            for (var i = 0; i < rightHandSide.Length; i++)
            {
                var residual = rightHandSide[i] - product[i];
                residualNormSquared += residual * residual;
            }

            return Math.Sqrt(residualNormSquared);
        }

        public double CalculateNorm(double[] vector)
        {
            var normSquared = 0.0;

            for (var i = 0; i < vector.Length; i++)
                normSquared += vector[i] * vector[i];

            return Math.Sqrt(normSquared);
        }
    }
}
