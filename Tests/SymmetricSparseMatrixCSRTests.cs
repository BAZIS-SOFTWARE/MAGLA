using System;
using CAESolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Тесты на сборку, доступ по глобальным индексам и умножение для
    /// SymmetricSparseMatrixCSR (хранение только верхнего треугольника).
    /// </summary>
    [TestClass]
    public class SymmetricSparseMatrixCSRTests
    {
        // Сборка глобальной матрицы жёсткости 1D-стержня из двух линейных
        // элементов (узлы 0-1-2), локальная матрица каждого элемента k*[[1,-1],[-1,1]].
        // ВАЖНО: внедиагональный вклад добавляется один раз (n0, n1), а не
        // дважды в обе стороны — матрица сама учитывает симметрию.
        private static SymmetricSparseMatrixCSR BuildTwoElementBarStiffness(double k)
        {
            var matrix = new SymmetricSparseMatrixCSR(3);

            void AddElementStiffness(int n0, int n1)
            {
                matrix.AddToElement(n0, n0, k);
                matrix.AddToElement(n0, n1, -k); // только одна сторона пары
                matrix.AddToElement(n1, n1, k);
            }

            AddElementStiffness(0, 1); // элемент 1: узлы 0-1
            AddElementStiffness(1, 2); // элемент 2: узлы 1-2

            matrix.FinalizeAssembly();
            return matrix;
        }

        [TestMethod]
        public void Assembly_AccumulatesContributionsAtSharedNode()
        {
            const double k = 2.5;
            var matrix = BuildTwoElementBarStiffness(k);

            Assert.AreEqual(2 * k, matrix[1, 1], 1e-12);
            Assert.AreEqual(k, matrix[0, 0], 1e-12);
            Assert.AreEqual(k, matrix[2, 2], 1e-12);

            Assert.AreEqual(-k, matrix[0, 1], 1e-12);
            Assert.AreEqual(-k, matrix[1, 2], 1e-12);

            Assert.AreEqual(0.0, matrix[0, 2], 1e-12);
            Assert.IsTrue(matrix.IsZero(0, 2));
        }

        [TestMethod]
        public void Indexer_IsSymmetric_RegardlessOfArgumentOrder()
        {
            var matrix = BuildTwoElementBarStiffness(1.0);

            Assert.AreEqual(matrix[0, 1], matrix[1, 0], 1e-12);
            Assert.AreEqual(matrix[1, 2], matrix[2, 1], 1e-12);
        }

        [TestMethod]
        public void AddToElement_BothDirectionsOfSamePair_DoubleCounts()
        {
            // Документирует контракт: индексы нормализуются, поэтому вклад
            // нужно добавлять один раз. Если добавить его для обеих сторон
            // пары (как для несимметричной матрицы), значение задвоится.
            var matrix = new SymmetricSparseMatrixCSR(2);

            matrix.AddToElement(0, 1, 3.0);
            matrix.AddToElement(1, 0, 3.0);

            matrix.FinalizeAssembly();

            Assert.AreEqual(6.0, matrix[0, 1], 1e-12);
        }

        [TestMethod]
        public void Assembly_CancellingContributions_AreTreatedAsZero()
        {
            var matrix = new SymmetricSparseMatrixCSR(2);

            matrix.AddToElement(0, 1, 3.0);
            matrix.AddToElement(1, 0, -3.0); // нормализуется в ту же позицию (0,1) и сокращается

            matrix.FinalizeAssembly();

            Assert.AreEqual(0.0, matrix[0, 1], 1e-12);
            Assert.IsTrue(matrix.IsZero(0, 1));
            Assert.AreEqual(0, matrix.NonZeroCount);
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
        public void GetRow_ReconstructsLowerTriangleFromSymmetry()
        {
            var matrix = BuildTwoElementBarStiffness(1.0);

            // Физически хранится только (0,1), но логическая строка 1
            // должна включать и восстановленный элемент (1,0).
            var row1 = matrix.GetRow(1);

            Assert.AreEqual(3, row1.Count);
            Assert.AreEqual(matrix[1, 0], row1[0], 1e-12);
            Assert.AreEqual(matrix[1, 1], row1[1], 1e-12);
            Assert.AreEqual(matrix[1, 2], row1[2], 1e-12);
        }

        [TestMethod]
        public void Indexer_OutOfRangeThrows()
        {
            var matrix = new SymmetricSparseMatrixCSR(3);
            matrix.FinalizeAssembly();

            Assert.ThrowsException<IndexOutOfRangeException>(() => { var _ = matrix[3, 0]; });
            Assert.ThrowsException<IndexOutOfRangeException>(() => { var _ = matrix[0, -1]; });
        }

        [TestMethod]
        public void AddToElement_AfterFinalize_UpdatesExistingPositionInPlace()
        {
            var matrix = new SymmetricSparseMatrixCSR(2);
            matrix.AddToElement(0, 1, 5.0);
            matrix.FinalizeAssembly();

            matrix.AddToElement(1, 0, 1.5); // обратный порядок — нормализуется в ту же позицию

            Assert.AreEqual(6.5, matrix[0, 1], 1e-12);
        }

        [TestMethod]
        public void AddToElement_AfterFinalize_NewPositionThrows()
        {
            var matrix = new SymmetricSparseMatrixCSR(2);
            matrix.AddToElement(0, 0, 5.0);
            matrix.FinalizeAssembly();

            Assert.ThrowsException<InvalidOperationException>(() => matrix.AddToElement(0, 1, 1.0));
        }

        [TestMethod]
        public void Multiply_BeforeFinalize_Throws()
        {
            var matrix = new SymmetricSparseMatrixCSR(2);
            matrix.AddToElement(0, 0, 1.0);

            Assert.ThrowsException<InvalidOperationException>(() => matrix.Multiply(new[] { 1.0, 1.0 }));
        }

        [TestMethod]
        public void CoordinateConstructor_SumsDuplicateEntriesRegardlessOfOrder()
        {
            var elements = new[]
            {
                (row: 0, col: 1, value: 2.0),
                (row: 1, col: 0, value: 3.0), // дубль в обратном порядке — суммируется в ту же позицию
                (row: 1, col: 1, value: 4.0),
            };

            var matrix = new SymmetricSparseMatrixCSR(2, elements);

            Assert.AreEqual(5.0, matrix[0, 1], 1e-12);
            Assert.AreEqual(4.0, matrix[1, 1], 1e-12);
            Assert.AreEqual(2, matrix.NonZeroCount);
        }
    }
}
