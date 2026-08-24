using CAESolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class JacobiPreconditionerTests
    {
        [TestMethod]
        public void Apply_DiagonalMatrix_DividesByDiagonal()
        {
            var matrix = BuildDiagonalMatrix(new[] { 2.0, 4.0, 8.0 });
            var preconditioner = new JacobiPreconditioner(matrix);
            var result = new double[3];

            preconditioner.Apply(new[] { 4.0, 12.0, -8.0 }, result);

            CollectionAssert.AreEqual(new[] { 2.0, 3.0, -1.0 }, result);
        }

        [TestMethod]
        public void Constructor_ZeroDiagonal_Throws()
        {
            var matrix = BuildDiagonalMatrix(new[] { 2.0, 0.0, 8.0 });

            Assert.ThrowsException<InvalidOperationException>(() => new JacobiPreconditioner(matrix));
        }

        [TestMethod]
        public void Apply_VectorLengthMismatch_Throws()
        {
            var preconditioner = new JacobiPreconditioner(BuildDiagonalMatrix(new[] { 2.0, 4.0 }));

            Assert.ThrowsException<ArgumentException>(() => preconditioner.Apply(new double[1], new double[2]));
            Assert.ThrowsException<ArgumentException>(() => preconditioner.Apply(new double[2], new double[1]));
        }

        [TestMethod]
        public void Solve_ProvidedPreconditioner_IsReusedAndHasPriorityOverFlag()
        {
            var matrix = BuildDiagonalMatrix(new[] { 2.0, 4.0, 8.0 });
            var preconditioner = new JacobiPreconditioner(matrix);
            var solver = new ConjugateGradientGaussPreSolver { RelativeTolerance = 1e-12, UsePreconditioner = false };
            var firstExpected = new[] { 1.0, -2.0, 3.0 };
            var secondExpected = new[] { -4.0, 5.0, 0.5 };

            var first = solver.Solve(new LinearSystem<SymmetricCSRMatrix>(matrix, matrix.Multiply(firstExpected)), null, preconditioner);
            Assert.AreEqual(1, solver.LastResult!.Iterations);
            var second = solver.Solve(new LinearSystem<SymmetricCSRMatrix>(matrix, matrix.Multiply(secondExpected)), null, preconditioner);
            Assert.AreEqual(1, solver.LastResult!.Iterations);

            CollectionAssert.AreEqual(firstExpected, first);
            CollectionAssert.AreEqual(secondExpected, second);
        }

        [TestMethod]
        public void Solve_PreconditionerForAnotherMatrix_Throws()
        {
            var matrix = BuildDiagonalMatrix(new[] { 2.0, 4.0 });
            var otherMatrix = BuildDiagonalMatrix(new[] { 2.0, 4.0 });
            var system = new LinearSystem<SymmetricCSRMatrix>(matrix, new[] { 2.0, 4.0 });
            var preconditioner = new JacobiPreconditioner(otherMatrix);

            Assert.ThrowsException<ArgumentException>(() => new ConjugateGradientGaussPreSolver().Solve(system, null, preconditioner));
        }

        private static SymmetricCSRMatrix BuildDiagonalMatrix(double[] diagonal)
        {
            var builder = new SymmetricCSRMatrixBuilder(diagonal.Length);
            for (var index = 0; index < diagonal.Length; index++)
                builder.AddToElement(index, index, diagonal[index]);

            return builder.Build();
        }
    }
}
