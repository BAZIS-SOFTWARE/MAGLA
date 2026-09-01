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
            var system = new LinearSystem(matrix, matrix.Multiply(expected));
            ILinearSolver solver = new ConjugateGradientGaussPreSolver
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
            var system = new LinearSystem(matrix, matrix.Multiply(expected));
            ILinearSolver solver = new SymmetricUtduSolver();

            var actual = solver.Solve(system);

            AssertSolution(expected, actual, 1e-10);
        }

        [TestMethod]
        public void Solve_ThroughInterface_ThrowsForIncompatibleMatrix()
        {
            var builder = new CSRMatrixBuilder(2, 2);
            builder.AddToElement(0, 0, 2.0);
            builder.AddToElement(1, 1, 3.0);
            var system = new LinearSystem(builder.Build(), new[] { 2.0, 3.0 });
            ILinearSolver solver = new ConjugateGradientGaussPreSolver();

            var exception = Assert.ThrowsException<ArgumentException>(() => solver.Solve(system));

            Assert.AreEqual("system", exception.ParamName);
        }

        [TestMethod]
        public void ConjugateGradientGaussPreSolver_ThroughInterface_ReturnsLastApproximationWhenNotConverged()
        {
            var matrix = BuildTridiagonalMatrix(4);
            double[] rightHandSide = { 1.0, 2.0, 3.0, 4.0 };
            var system = new LinearSystem(matrix, rightHandSide);
            var conjugateGradient = new ConjugateGradientGaussPreSolver
            {
                RelativeTolerance = 1e-30,
                MaxIterations = 1,
                UsePreconditioner = false
            };
            ILinearSolver solver = conjugateGradient;

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
