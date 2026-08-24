using TaskSolverCore;

namespace Tests;

[TestClass]
public class HeatTransportOptionsTests
{
    [TestMethod]
    public void NortonFluxUsesRelativeLinearVelocity()
    {
        var options = new FrictionHeatOptions
        {
            Beta = 2.0,
            Consistency = 3.0,
            VelocityExponent = 1.0,
            HeatPartition = 0.5,
            ToolLinearVelocity = [4.0, 0.0, 0.0]
        };

        var flux = options.CalculateFlux(0.0, 0.0, 0.0, [1.0, 0.0, 0.0]);

        Assert.AreEqual(27.0, flux, 1e-12);
    }

    [TestMethod]
    public void NortonFluxAccountsForToolRotation()
    {
        var options = new FrictionHeatOptions
        {
            Beta = 1.0,
            Consistency = 2.0,
            VelocityExponent = 0.0,
            HeatPartition = 0.25,
            ToolAngularVelocity = [0.0, 0.0, 10.0],
            RotationCenter = [1.0, 1.0, 0.0]
        };

        var flux = options.CalculateFlux(1.0, 3.0, 0.0, [0.0, 0.0, 0.0]);

        Assert.AreEqual(10.0, flux, 1e-12);
    }

    [TestMethod]
    public void ZeroRelativeVelocityProducesNoHeatForNortonLaw()
    {
        var options = new FrictionHeatOptions { Beta = 10.0, Consistency = 20.0, VelocityExponent = 0.0 };

        var flux = options.CalculateFlux(0.0, 0.0, 0.0, [0.0, 0.0, 0.0]);

        Assert.AreEqual(0.0, flux, 1e-12);
    }

    [TestMethod]
    public void HeatPartitionCanBeCalculatedFromEffusivities()
    {
        var options = new FrictionHeatOptions { Beta = 2.0, Consistency = 4.0, MaterialEffusivity = 3.0, ToolEffusivity = 1.0, ToolLinearVelocity = [2.0, 0.0, 0.0] };

        var flux = options.CalculateFlux(0.0, 0.0, 0.0, [0.0, 0.0, 0.0]);

        Assert.AreEqual(12.0, flux, 1e-12);
    }
}
