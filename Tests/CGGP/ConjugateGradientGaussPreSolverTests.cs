using System;
using CAESolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Тесты решателя ConjugateGradientGaussPreSolver (PCG с якоби-предобуславливанием)
    /// на симметричных положительно определённых системах, собранных через
    /// SymmetricSparseMatrixCSRBuilder.
    ///
    /// Схожие сценарии объединены в один [DataTestMethod] с несколькими
    /// [DataRow] — один и тот же код теста прогоняется на разных входных
    /// данных (MSTest создаёт отдельный результат в Test Explorer на
    /// каждую строку, с читаемым именем через DisplayName).
    /// </summary>
    [TestClass]
    public class ConjugateGradientGaussPreSolverTests
    {
        // Диагональная матрица: для неё якоби-предобуславливатель M = diag(A)
        // совпадает с самой матрицей (P^-1 A = I), поэтому PCG обязан
        // сойтись ровно за одну итерацию из нулевого приближения — удобно
        // для точных (а не только "сошлось в пределах допуска") проверок.
        private static SymmetricCSRMatrix BuildDiagonalMatrix(double[] diagonal)
        {
            var builder = new SymmetricCSRMatrixBuilder(diagonal.Length);
            for (int i = 0; i < diagonal.Length; i++)
                builder.AddToElement(i, i, diagonal[i]);

            return builder.Build();
        }

        // Симметричная трёхдиагональная матрица размера n со значением
        // diagonal на главной диагонали и offDiagonal на соседних —
        // диагонально доминантна (|diagonal| > 2*|offDiagonal|), значит СПД.
        private static SymmetricCSRMatrix BuildTridiagonalMatrix(int n, double diagonal, double offDiagonal)
        {
            var builder = new SymmetricCSRMatrixBuilder(n);
            for (int i = 0; i < n; i++)
                builder.AddToElement(i, i, diagonal);

            for (int i = 0; i < n - 1; i++)
                builder.AddToElement(i, i + 1, offDiagonal);

            return builder.Build();
        }

        /// <summary>
        /// Три сценария на диагональных системах, где решение известно точно
        /// (b строится как A * expectedSolution): сходимость за 1 итерацию
        /// из нулевого приближения, немедленная сходимость, когда начальное
        /// приближение уже точное решение, и нулевая правая часть.
        /// </summary>
        [DataTestMethod]
        [DataRow(new double[] { 2.0, 4.0, 5.0 }, new double[] { 3.0, 1.0, 2.0 }, null, 1,
            DisplayName = "Из нулевого приближения — точная сходимость за 1 итерацию")]
        [DataRow(new double[] { 2.0, 3.0 }, new double[] { 5.0, -2.0 }, new double[] { 5.0, -2.0 }, 0,
            DisplayName = "Начальное приближение уже точное решение")]
        [DataRow(new double[] { 4.0, 4.0, 4.0 }, new double[] { 0.0, 0.0, 0.0 }, null, 0,
            DisplayName = "Нулевая правая часть — тривиальное решение без итераций")]
        public void Solve_DiagonalSystem_MatchesExpectedIterationsAndSolution(
            double[] diagonal, double[] expectedSolution, double[]? initialGuess, int expectedIterations)
        {
            var matrix = BuildDiagonalMatrix(diagonal);
            var b = matrix.Multiply(expectedSolution);
            var system = new LinearSystem<SymmetricCSRMatrix>(matrix, b);

            var solver = new ConjugateGradientGaussPreSolver();
            var solution = solver.Solve(system, initialGuess);
            var result = solver.LastResult!;

            Assert.IsTrue(result.Converged);
            Assert.AreEqual(expectedIterations, result.Iterations);

            for (int i = 0; i < expectedSolution.Length; i++)
                Assert.AreEqual(expectedSolution[i], solution[i], 1e-9);
        }

        /// <summary>
        /// Сходимость на трёхдиагональной СПД-системе — как с якоби-
        /// предобуславливанием, так и без него (UsePreconditioner = false
        /// вырождает решатель в классический CG). В обоих случаях решение
        /// должно удовлетворять A x ~= b в пределах допуска.
        /// </summary>
        [DataTestMethod]
        [DataRow(true, DisplayName = "С якоби-предобуславливанием")]
        [DataRow(false, DisplayName = "Без предобуславливания")]
        public void Solve_TridiagonalSpdSystem_ConvergesToCorrectSolution(bool usePreconditioner)
        {
            var matrix = BuildTridiagonalMatrix(5, diagonal: 4.0, offDiagonal: -1.0);
            var b = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var system = new LinearSystem<SymmetricCSRMatrix>(matrix, b);

            var solver = new ConjugateGradientGaussPreSolver { RelativeTolerance = 1e-10, UsePreconditioner = usePreconditioner };
            var solution = solver.Solve(system);
            var result = solver.LastResult!;

            Assert.IsTrue(result.Converged);

            var residual = matrix.Multiply(solution);
            for (int i = 0; i < b.Length; i++)
                Assert.AreEqual(b[i], residual[i], 1e-6);
        }

        /// <summary>
        /// Solve должен бросать ArgumentException, если длина b или
        /// initialGuess не совпадает с размером матрицы — независимо от
        /// того, какой из двух векторов не совпал по длине.
        /// </summary>
        [DataTestMethod]
        [DataRow(false, DisplayName = "Несовпадение длины b")]
        [DataRow(true, DisplayName = "Несовпадение длины начального приближения")]
        public void Solve_VectorLengthMismatch_Throws(bool mismatchInitialGuess)
        {
            var matrix = BuildDiagonalMatrix(new[] { 1.0, 2.0, 3.0 });
            var b = mismatchInitialGuess ? new[] { 1.0, 2.0, 3.0 } : new[] { 1.0, 2.0 };
            var initialGuess = mismatchInitialGuess ? new[] { 0.0, 0.0 } : null;

            var solver = new ConjugateGradientGaussPreSolver();

            if (mismatchInitialGuess)
            {
                var system = new LinearSystem<SymmetricCSRMatrix>(matrix, b);
                Assert.ThrowsException<ArgumentException>(() => solver.Solve(system, initialGuess));
            }
            else
            {
                Assert.ThrowsException<ArgumentException>(() => new LinearSystem<SymmetricCSRMatrix>(matrix, b));
            }
        }
    }
}
