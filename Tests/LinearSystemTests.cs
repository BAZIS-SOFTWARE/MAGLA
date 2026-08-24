using CAESolvers;

namespace Tests
{
    [TestClass]
    public class LinearSystemTests
    {
        [TestMethod]
        public void Constructor_ExposesTypedAndUntypedMatrix()
        {
            var matrix = BuildSymmetricMatrix(2);
            var rightHandSide = new[] { 1.0, 2.0 };
            var system = new LinearSystem<SymmetricCSRMatrix>(matrix, rightHandSide);
            var untypedSystem = (ILinearSystem)system;

            Assert.AreSame(matrix, system.Matrix);
            Assert.AreSame(matrix, untypedSystem.Matrix);
            Assert.AreSame(rightHandSide, system.RightHandSide);
            Assert.AreSame(rightHandSide, untypedSystem.RightHandSide);
        }

        [TestMethod]
        public void Constructor_SupportsGeneralCsrMatrix()
        {
            var builder = new CSRMatrixBuilder(2, 2);
            builder.AddToElement(0, 0, 2.0);
            builder.AddToElement(0, 1, -1.0);
            builder.AddToElement(1, 0, 1.0);
            builder.AddToElement(1, 1, 3.0);

            var matrix = builder.Build();
            var system = new LinearSystem<CSRMatrix>(matrix, new[] { 4.0, 5.0 });

            Assert.AreSame(matrix, system.Matrix);
            Assert.AreEqual(2, system.RightHandSide.Length);
        }

        [TestMethod]
        public void Constructor_ThrowsForNullMatrix()
        {
            var exception = Assert.ThrowsException<ArgumentNullException>(() => new LinearSystem<SymmetricCSRMatrix>(null!, Array.Empty<double>()));

            Assert.AreEqual("matrix", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_ThrowsForNullRightHandSide()
        {
            var matrix = BuildSymmetricMatrix(1);
            var exception = Assert.ThrowsException<ArgumentNullException>(() => new LinearSystem<SymmetricCSRMatrix>(matrix, null!));

            Assert.AreEqual("rightHandSide", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_ThrowsWhenRightHandSideLengthDoesNotMatchRowCount()
        {
            var matrix = BuildSymmetricMatrix(2);
            var exception = Assert.ThrowsException<ArgumentException>(() => new LinearSystem<SymmetricCSRMatrix>(matrix, new[] { 1.0 }));

            Assert.AreEqual("rightHandSide", exception.ParamName);
        }

        private static SymmetricCSRMatrix BuildSymmetricMatrix(int size)
        {
            var builder = new SymmetricCSRMatrixBuilder(size);

            for (var index = 0; index < size; index++)
                builder.AddToElement(index, index, 1.0);

            return builder.Build();
        }
    }
}
