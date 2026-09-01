using CAESolvers;

namespace Tests
{
    [TestClass]
    public class Ilu0PreconditionerTests
    {
        [TestMethod]
        public void Apply_ReproducesFactorsFromCgComparisonWorkbook()
        {
            var matrix = CgComparisonData.BuildMatrix();
            var preconditioner = new Ilu0Preconditioner(matrix);
            var expected = new[] { 1.0, -2.0, 3.0, -4.0, 5.0, -6.0, 7.0, -8.0 };
            var upperProduct = CgComparisonData.Multiply(CgComparisonData.UpperFactor, expected);
            var rightHandSide = CgComparisonData.Multiply(CgComparisonData.LowerFactor, upperProduct);
            var actual = new double[expected.Length];

            preconditioner.Apply(rightHandSide, actual);

            for (var index = 0; index < expected.Length; index++)
                Assert.AreEqual(expected[index], actual[index], 1e-11);
        }

        [TestMethod]
        public void Constructor_ThrowsWhenIlu0ProducesZeroPivot()
        {
            var builder = new CSRMatrixBuilder(2, 2);
            builder.AddToElement(0, 0, 1.0);
            builder.AddToElement(0, 1, 1.0);
            builder.AddToElement(1, 0, 1.0);
            builder.AddToElement(1, 1, 1.0);
            var matrix = builder.Build();

            Assert.ThrowsException<InvalidOperationException>(() => new Ilu0Preconditioner(matrix));
        }
    }
}
