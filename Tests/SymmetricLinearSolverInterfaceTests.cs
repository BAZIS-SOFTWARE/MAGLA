using CAESolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class SymmetricLinearSolverInterfaceTests
    {
        [TestMethod]
        public void Solve_ThroughInterface_WorksForConjugateGradientGaussPreSolver()
        {
            var matrix = BuildTridiagonalMatrix(4);
            double[] expected = { 1.0, -2.0, 3.0, 0.5 };
            var system = new LinearSystem<SymmetricCSRMatrix>(matrix, matrix.Multiply(expected));
            ISymmetricLinearSolver solver = new ConjugateGradientGaussPreSolver
            {
                RelativeTolerance = 1e-12
            };

            var actual = solver.Solve(system);

            AssertSolution(expected, actual, 1e-10);
        }

        [TestMethod]
        public void Solve_ThroughInterface_WorksForSymmetricUtduSolver()
        {
            var matrix = BuildTridiagonalMatrix(4);
            double[] expected = { 1.0, -2.0, 3.0, 0.5 };
            var system = new LinearSystem<SymmetricCSRMatrix>(matrix, matrix.Multiply(expected));
            ISymmetricLinearSolver solver = new SymmetricUtduSolver();

            var actual = solver.Solve(system);

            AssertSolution(expected, actual, 1e-10);
        }

        [TestMethod]
        public void ConjugateGradientGaussPreSolver_ThroughInterface_ReturnsLastApproximationWhenNotConverged()
        {
            var matrix = BuildTridiagonalMatrix(4);
            double[] rightHandSide = { 1.0, 2.0, 3.0, 4.0 };
            var system = new LinearSystem<SymmetricCSRMatrix>(matrix, rightHandSide);
            var conjugateGradient = new ConjugateGradientGaussPreSolver
            {
                RelativeTolerance = 1e-30,
                MaxIterations = 1,
                UsePreconditioner = false
            };
            ISymmetricLinearSolver solver = conjugateGradient;

            var solution = solver.Solve(system);
            var result = conjugateGradient.LastResult!;

            Assert.AreSame(solution, result.Solution);
            Assert.IsFalse(result.Converged);
            Assert.AreEqual(1, result.Iterations);
            Assert.IsTrue(result.ResidualNorm > 0.0);
        }

        private static SymmetricCSRMatrix BuildTridiagonalMatrix(int size)
        {
            var builder = new SymmetricCSRMatrixBuilder(size);
            for (int i = 0; i < size; i++)
                builder.AddToElement(i, i, 4.0);

            for (int i = 0; i < size - 1; i++)
                builder.AddToElement(i, i + 1, -1.0);

            return builder.Build();
        }

        private static void AssertSolution(double[] expected, double[] actual, double tolerance)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], tolerance);
        }
    }
}
