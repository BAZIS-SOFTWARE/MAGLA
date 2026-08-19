using System;
using System.Linq;
using CAESolvers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    /// <summary>
    /// Тесты <see cref="SymmetricPatternGraph"/>: корректность построения
    /// графа смежности из матрицы (FromMatrix) и точечных методов доступа
    /// GetDegree/GetNeighbors.
    /// </summary>
    [TestClass]
    public class SymmetricPatternGraphTests
    {
        private static SymmetricCSRMatrix BuildDiagonalMatrix(int n)
        {
            var builder = new SymmetricCSRMatrixBuilder(n);
            for (int i = 0; i < n; i++)
                builder.AddToElement(i, i, 1.0);

            return builder.Build();
        }

        private static SymmetricCSRMatrix BuildChainMatrix(int n)
        {
            var builder = new SymmetricCSRMatrixBuilder(n);
            for (int i = 0; i < n; i++)
                builder.AddToElement(i, i, 4.0);
            for (int i = 0; i + 1 < n; i++)
                builder.AddToElement(i, i + 1, -1.0);

            return builder.Build();
        }

        private static SymmetricCSRMatrix BuildRandomMatrix(int n, double density, int seed)
        {
            var builder = new SymmetricCSRMatrixBuilder(n);
            var rng = new Random(seed);
            for (int i = 0; i < n; i++)
                builder.AddToElement(i, i, 10.0);

            for (int row = 0; row < n; row++)
            {
                for (int col = row + 1; col < n; col++)
                {
                    if (rng.NextDouble() < density)
                        builder.AddToElement(row, col, -1.0);
                }
            }

            return builder.Build();
        }

        /// <summary>
        /// Брутфорсная проверка: для каждой вершины i множество соседей,
        /// возвращаемое GetNeighbors, должно совпадать с множеством j, для
        /// которых A[i,j] != 0 (и i != j), а GetDegree — с размером этого
        /// множества. Строится независимо от FromMatrix, через прямой обход
        /// матрицы по индексатору.
        /// </summary>
        [DataTestMethod]
        [DataRow(0, DisplayName = "Диагональная матрица (нет рёбер)")]
        [DataRow(1, DisplayName = "Цепочка")]
        [DataRow(2, DisplayName = "Случайная разреженная структура")]
        public void GetDegreeAndGetNeighbors_MatchBruteForceAdjacency(int scenario)
        {
            SymmetricCSRMatrix matrix = scenario switch
            {
                0 => BuildDiagonalMatrix(40),
                1 => BuildChainMatrix(40),
                _ => BuildRandomMatrix(80, 0.05, seed: 5),
            };

            var graph = SymmetricPatternGraph.FromMatrix(matrix);

            for (int i = 0; i < matrix.Size; i++)
            {
                var expectedNeighbors = Enumerable.Range(0, matrix.Size)
                    .Where(j => j != i && matrix[i, j] != 0.0)
                    .OrderBy(j => j)
                    .ToArray();

                Assert.AreEqual(expectedNeighbors.Length, graph.GetDegree(i),
                    $"Неверная степень вершины {i}.");

                var actualNeighbors = graph.GetNeighbors(i).ToArray().OrderBy(j => j).ToArray();
                CollectionAssert.AreEqual(expectedNeighbors, actualNeighbors,
                    $"Неверный список соседей вершины {i}.");
            }
        }

        /// <summary>
        /// GetDegree обязан согласовываться с прямым вычислением через
        /// Pointers (то определение, которое он инкапсулирует), а сумма
        /// степеней всех вершин — с удвоенным числом рёбер: каждое ребро
        /// хранится в списках смежности обеих своих вершин.
        /// </summary>
        [TestMethod]
        public void GetDegree_SumEqualsTwiceEdgeCount()
        {
            var matrix = BuildRandomMatrix(120, 0.03, seed: 17);
            var graph = SymmetricPatternGraph.FromMatrix(matrix);

            long sumOfDegrees = 0;
            for (int i = 0; i < graph.Size; i++)
            {
                Assert.AreEqual(graph.Pointers[i + 1] - graph.Pointers[i], graph.GetDegree(i));
                sumOfDegrees += graph.GetDegree(i);
            }

            Assert.AreEqual(2L * graph.EdgeCount, sumOfDegrees);
        }

        /// <summary>
        /// GetNeighbors не должен копировать данные: срез должен указывать
        /// на тот же самый массив Neighbors, что и хранится внутри графа —
        /// именно это отличает Span-подход от возврата нового int[] и
        /// является частью контракта метода (см. обсуждение в переписке).
        /// </summary>
        [TestMethod]
        public unsafe void GetNeighbors_IsAViewOverTheUnderlyingArray_NoCopy()
        {
            var matrix = BuildChainMatrix(10);
            var graph = SymmetricPatternGraph.FromMatrix(matrix);

            var span = graph.GetNeighbors(3);

            fixed (int* spanPtr = span)
            fixed (int* arrayPtr = graph.Neighbors)
            {
                IntPtr expected = (IntPtr)(arrayPtr + graph.Pointers[3]);
                IntPtr actual = (IntPtr)spanPtr;
                Assert.AreEqual(expected, actual,
                    "GetNeighbors должен возвращать срез исходного массива Neighbors, а не копию.");
            }
        }

        /// <summary>
        /// Изолированные вершины (диагональная матрица без внедиагональных
        /// элементов) должны иметь нулевую степень и пустой список соседей —
        /// граничный случай, который легко сломать off-by-one ошибкой в
        /// Pointers.
        /// </summary>
        [TestMethod]
        public void GetDegreeAndGetNeighbors_IsolatedVertex_ZeroDegreeEmptyList()
        {
            var matrix = BuildDiagonalMatrix(15);
            var graph = SymmetricPatternGraph.FromMatrix(matrix);

            for (int i = 0; i < graph.Size; i++)
            {
                Assert.AreEqual(0, graph.GetDegree(i));
                Assert.AreEqual(0, graph.GetNeighbors(i).Length);
            }
        }

        /// <summary>
        /// Последняя вершина — граничный случай для Pointers[vertex + 1],
        /// который легко перепутать с выходом за границу массива.
        /// </summary>
        [TestMethod]
        public void GetNeighbors_LastVertex_DoesNotThrow()
        {
            var matrix = BuildChainMatrix(20);
            var graph = SymmetricPatternGraph.FromMatrix(matrix);

            var neighbors = graph.GetNeighbors(graph.Size - 1).ToArray();

            CollectionAssert.AreEqual(new[] { graph.Size - 2 }, neighbors);
            Assert.AreEqual(1, graph.GetDegree(graph.Size - 1));
        }

        [DataTestMethod]
        [DataRow(-1, DisplayName = "Отрицательный индекс")]
        [DataRow(int.MinValue, DisplayName = "Минимальное значение int")]
        public void GetDegree_IndexBelowRange_Throws(int vertex)
        {
            var graph = SymmetricPatternGraph.FromMatrix(BuildChainMatrix(10));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => graph.GetDegree(vertex));
        }

        [TestMethod]
        public void GetDegree_IndexEqualToSize_Throws()
        {
            var graph = SymmetricPatternGraph.FromMatrix(BuildChainMatrix(10));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => graph.GetDegree(graph.Size));
        }

        [DataTestMethod]
        [DataRow(-1, DisplayName = "Отрицательный индекс")]
        [DataRow(int.MinValue, DisplayName = "Минимальное значение int")]
        public void GetNeighbors_IndexBelowRange_Throws(int vertex)
        {
            var graph = SymmetricPatternGraph.FromMatrix(BuildChainMatrix(10));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => graph.GetNeighbors(vertex));
        }

        [TestMethod]
        public void GetNeighbors_IndexEqualToSize_Throws()
        {
            var graph = SymmetricPatternGraph.FromMatrix(BuildChainMatrix(10));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => graph.GetNeighbors(graph.Size));
        }
    }
}
