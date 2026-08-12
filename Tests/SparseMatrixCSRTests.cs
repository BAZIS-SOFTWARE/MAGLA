using System;
using CAESolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Тесты на сборку (через SparseMatrixCSRBuilder) и доступ по глобальным
    /// индексам для готовой SparseMatrixCSR.
    /// </summary>
    [TestClass]
    public class SparseMatrixCSRTests
    {
        // Сборка глобальной матрицы жёсткости 1D-стержня из двух линейных
        // элементов (узлы 0-1-2), локальная матрица каждого элемента k*[[1,-1],[-1,1]].
        // Узел 1 общий для обоих элементов, поэтому вклад в K[1,1] должен
        // сложиться из двух элементов: k + k = 2k.
        private static SparseMatrixCSR BuildTwoElementBarStiffness(double k)
        {
            var builder = new SparseMatrixCSRBuilder(3, 3);

            void AddElementStiffness(int n0, int n1)
            {
                builder.AddToElement(n0, n0, k);
                builder.AddToElement(n0, n1, -k);
                builder.AddToElement(n1, n0, -k);
                builder.AddToElement(n1, n1, k);
            }

            AddElementStiffness(0, 1); // элемент 1: узлы 0-1
            AddElementStiffness(1, 2); // элемент 2: узлы 1-2

            return builder.Build();
        }

        [TestMethod]
        public void Assembly_AccumulatesContributionsAtSharedNode()
        {
            const double k = 2.5;
            var matrix = BuildTwoElementBarStiffness(k);

            // Общий узел 1 получает вклад от обоих элементов
            Assert.AreEqual(2 * k, matrix[1, 1], 1e-12);

            // Крайние узлы получают вклад только от одного элемента
            Assert.AreEqual(k, matrix[0, 0], 1e-12);
            Assert.AreEqual(k, matrix[2, 2], 1e-12);

            // Внедиагональные связи
            Assert.AreEqual(-k, matrix[0, 1], 1e-12);
            Assert.AreEqual(-k, matrix[1, 0], 1e-12);
            Assert.AreEqual(-k, matrix[1, 2], 1e-12);
            Assert.AreEqual(-k, matrix[2, 1], 1e-12);

            // Несуществующая связь между несмежными узлами 0 и 2 — точный ноль
            Assert.AreEqual(0.0, matrix[0, 2], 1e-12);
            Assert.IsTrue(matrix.IsZero(0, 2));
        }

        [TestMethod]
        public void Assembly_CancellingContributions_AreTreatedAsZero()
        {
            var builder = new SparseMatrixCSRBuilder(2, 2);

            builder.AddToElement(0, 1, 3.0);
            builder.AddToElement(0, 1, -3.0); // суммарный вклад равен нулю

            var matrix = builder.Build();

            Assert.AreEqual(0.0, matrix[0, 1], 1e-12);
            Assert.IsTrue(matrix.IsZero(0, 1));
            Assert.AreEqual(0, matrix.NonZeroCount);
        }

        [TestMethod]
        public void GetDiagonal_ReturnsDiagonalValuesForEachRow()
        {
            const double k = 2.5;
            var matrix = BuildTwoElementBarStiffness(k);

            Assert.AreEqual(k, matrix.GetDiagonal(0), 1e-12);
            Assert.AreEqual(2 * k, matrix.GetDiagonal(1), 1e-12);
            Assert.AreEqual(k, matrix.GetDiagonal(2), 1e-12);
        }

        [TestMethod]
        public void GetDiagonal_StructurallyZeroDiagonal_ReturnsZero()
        {
            var builder = new SparseMatrixCSRBuilder(2, 2);
            builder.AddToElement(0, 1, 5.0); // диагональ строки 0 не участвует в сборке

            var matrix = builder.Build();

            Assert.AreEqual(0.0, matrix.GetDiagonal(0), 1e-12);
        }

        [TestMethod]
        public void Multiply_MatchesAnalyticResultForBarStiffness()
        {
            const double k = 1.0;
            var matrix = BuildTwoElementBarStiffness(k);

            // K * [1, 1, 1] = 0 (жёсткое смещение не создаёт усилий)
            var rigidBodyResult = matrix.Multiply(new[] { 1.0, 1.0, 1.0 });
            CollectionAssert.AreEqual(new[] { 0.0, 0.0, 0.0 }, rigidBodyResult);

            // K * [0, 1, 0] = [-k, 2k, -k]
            var unitDisplacement = matrix.Multiply(new[] { 0.0, 1.0, 0.0 });
            CollectionAssert.AreEqual(new[] { -k, 2 * k, -k }, unitDisplacement);
        }

        [TestMethod]
        public void GetRow_ReturnsOnlyNonZeroColumnsForRow()
        {
            var matrix = BuildTwoElementBarStiffness(1.0);

            var row1 = matrix.GetRow(1);

            Assert.AreEqual(3, row1.Count);
            Assert.IsTrue(row1.ContainsKey(0));
            Assert.IsTrue(row1.ContainsKey(1));
            Assert.IsTrue(row1.ContainsKey(2));
        }

        [TestMethod]
        public void Indexer_OutOfRangeThrows()
        {
            var matrix = new SparseMatrixCSRBuilder(3, 3).Build();

            Assert.ThrowsException<IndexOutOfRangeException>(() => { var _ = matrix[3, 0]; });
            Assert.ThrowsException<IndexOutOfRangeException>(() => { var _ = matrix[0, -1]; });
        }

        [TestMethod]
        public void AccumulateAt_UpdatesExistingPositionInPlace()
        {
            var builder = new SparseMatrixCSRBuilder(2, 2);
            builder.AddToElement(0, 0, 5.0);
            var matrix = builder.Build();

            matrix.AccumulateAt(0, 0, 1.5); // например, повторная сборка на новой итерации решателя

            Assert.AreEqual(6.5, matrix[0, 0], 1e-12);
        }

        [TestMethod]
        public void AccumulateAt_NewPositionThrows()
        {
            var builder = new SparseMatrixCSRBuilder(2, 2);
            builder.AddToElement(0, 0, 5.0);
            var matrix = builder.Build();

            // Позиция (0,1) не входила в структуру разреженности при сборке —
            // структуру после построения матрицы менять нельзя.
            Assert.ThrowsException<InvalidOperationException>(() => matrix.AccumulateAt(0, 1, 1.0));
        }

        [TestMethod]
        public void Indexer_Set_NewPositionThrows()
        {
            var builder = new SparseMatrixCSRBuilder(2, 2);
            builder.AddToElement(0, 0, 5.0);
            var matrix = builder.Build();

            Assert.ThrowsException<InvalidOperationException>(() => matrix[0, 1] = 1.0);
        }

        [TestMethod]
        public void CoordinateConstructor_SumsDuplicateEntries()
        {
            var elements = new[]
            {
                new MatrixEntry(0, 0, 2.0),
                new MatrixEntry(0, 0, 3.0), // дубль — должен суммироваться, а не перезаписываться
                new MatrixEntry(1, 1, 4.0),
            };

            var matrix = new SparseMatrixCSR(2, 2, elements);

            Assert.AreEqual(5.0, matrix[0, 0], 1e-12);
            Assert.AreEqual(4.0, matrix[1, 1], 1e-12);
            Assert.AreEqual(2, matrix.NonZeroCount);
        }
    }
}
