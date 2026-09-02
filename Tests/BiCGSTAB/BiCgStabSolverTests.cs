using CAESolvers;

namespace Tests
{
    [TestClass]
    public class BiCgStabSolverTests
    {
        [TestMethod]
        public void Solve_CgComparisonSystem_ConvergesWithinThreeIterations()
        {
            var matrix = CgComparisonData.BuildMatrix();
            var system = new LinearSystem(matrix, CgComparisonData.RightHandSide);
            var preconditioner = new Ilu0Preconditioner(matrix);
            var solver = new BiCgStabSolver { RelativeTolerance = 1e-12, MaxIterations = 3 };

            var solution = solver.SolveWithInitialGuess(system, new double[matrix.RowCount], preconditioner);
            var result = solver.LastResult!;

            Assert.IsTrue(result.Converged, $"Итерации: {result.Iterations}, норма: {result.ResidualNorm:E16}");
            Assert.IsTrue(result.Iterations <= 3);

            for (var index = 0; index < solution.Length; index++)
                Assert.AreEqual(CgComparisonData.ExactSolution[index], solution[index], 1e-10);

            var rightHandSideNorm = Norm(CgComparisonData.RightHandSide);
            var expectedRelativeResidual = result.ResidualNorm / rightHandSideNorm;
            Assert.AreEqual(expectedRelativeResidual, result.RelativeResidual, 1e-15);
            Assert.IsTrue(result.RelativeResidual <= solver.RelativeTolerance);
        }

        [TestMethod]
        public void Solve_CgComparisonSystem_ConvergesToExactSolution()
        {
            var matrix = CgComparisonData.BuildMatrix();
            var system = new LinearSystem(matrix, CgComparisonData.RightHandSide);
            var solver = new BiCgStabSolver { RelativeTolerance = 1e-12, MaxIterations = 100 };

            var solution = solver.Solve(system);

            Assert.IsTrue(solver.LastResult!.Converged);
            for (var index = 0; index < solution.Length; index++)
                Assert.AreEqual(CgComparisonData.ExactSolution[index], solution[index], 1e-10);
        }

        [DataTestMethod]
        [DataRow(true, DisplayName = "Несимметричная система с ILU(0)")]
        [DataRow(false, DisplayName = "Несимметричная система без предобуславливателя")]
        public void Solve_NonsymmetricSystem_ConvergesThroughLinearSolverInterface(bool usePreconditioner)
        {
            var matrix = BuildNonsymmetricMatrix();
            var expected = new[] { 1.0, -2.0, 3.0 };
            var system = new LinearSystem(matrix, matrix.Multiply(expected));
            ILinearSolver solver = new BiCgStabSolver { RelativeTolerance = 1e-12, MaxIterations = 20, UsePreconditioner = usePreconditioner };

            var solution = solver.Solve(system);

            for (var index = 0; index < expected.Length; index++)
                Assert.AreEqual(expected[index], solution[index], 1e-10);
        }

        [TestMethod]
        public void Solve_ExactInitialGuess_CompletesWithoutIterations()
        {
            var matrix = BuildNonsymmetricMatrix();
            var expected = new[] { 1.0, -2.0, 3.0 };
            var system = new LinearSystem(matrix, matrix.Multiply(expected));
            var solver = new BiCgStabSolver { RelativeTolerance = 1e-12 };

            var solution = solver.SolveWithInitialGuess(system, expected);

            CollectionAssert.AreEqual(expected, solution);
            Assert.AreEqual(0, solver.LastResult!.Iterations);
            Assert.IsTrue(solver.LastResult.Converged);
        }

        [TestMethod]
        public void Solve_NegativeMaxIterations_Throws()
        {
            var matrix = BuildNonsymmetricMatrix();
            var system = new LinearSystem(matrix, new double[matrix.RowCount]);
            var solver = new BiCgStabSolver { MaxIterations = -1 };

            var exception = Assert.ThrowsException<ArgumentOutOfRangeException>(() => solver.Solve(system));

            Assert.AreEqual(nameof(solver.MaxIterations), exception.ParamName);
        }

        private static CSRMatrix BuildNonsymmetricMatrix()
        {
            var values = new[]
            {
                new[] { 4.0, 1.0, 0.0 },
                new[] { 2.0, 3.0, 1.0 },
                new[] { 0.0, -1.0, 2.0 }
            };
            var builder = new CSRMatrixBuilder(3, 3);

            for (var row = 0; row < values.Length; row++)
            {
                for (var column = 0; column < values[row].Length; column++)
                {
                    if (values[row][column] != 0.0)
                        builder.AddToElement(row, column, values[row][column]);
                }
            }

            return builder.Build();
        }

        private static double Norm(double[] vector)
        {
            var squaredNorm = 0.0;
            for (var index = 0; index < vector.Length; index++)
                squaredNorm += vector[index] * vector[index];

            return Math.Sqrt(squaredNorm);
        }
    }
}
