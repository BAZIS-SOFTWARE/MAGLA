namespace TaskSolverCore
{
    /// <summary>Параметры конвективного переноса и контактного тепловыделения.</summary>
    public class HeatTransportOptions
    {
        public HeatConvectionOptions Convection { get; set; } = new();
        public FrictionHeatOptions FrictionHeat { get; set; } = new();

        internal void Validate(int dimensions, bool convectionEnabled)
        {
            if (dimensions is < 2 or > 3)
                throw new ArgumentOutOfRangeException(nameof(dimensions));

            Convection.Validate(dimensions, convectionEnabled);
            FrictionHeat.Validate();
        }
    }

    /// <summary>Параметры поля скорости в уравнении энергии.</summary>
    public class HeatConvectionOptions
    {
        public double[] Velocity { get; set; } = [0.0, 0.0, 0.0];
        public Dictionary<int, double[]> ElementVelocities { get; set; } = [];
        public Dictionary<int, double[]> NodeVelocities { get; set; } = [];

        internal void Validate(int dimensions, bool enabled)
        {
            if (!enabled)
                return;
            if (Velocity == null || Velocity.Length < dimensions)
                throw new ArgumentException($"Для {dimensions}D-конвекции требуется не менее {dimensions} компонент скорости.");
            if (Velocity.Take(dimensions).Any(value => !double.IsFinite(value)))
                throw new ArgumentException("Компоненты скорости конвекции должны быть конечными числами.");
            foreach (var item in ElementVelocities.Concat(NodeVelocities))
                if (item.Value == null || item.Value.Length < dimensions || item.Value.Take(dimensions).Any(value => !double.IsFinite(value)))
                    throw new ArgumentException($"Скорость для объекта {item.Key} должна содержать не менее {dimensions} конечных компонент.");
        }

        internal double[] ResolveVelocity(int elementNumber, IEnumerable<int> nodeNumbers)
        {
            if (ElementVelocities.TryGetValue(elementNumber, out var elementVelocity))
                return elementVelocity;

            var nodal = nodeNumbers.Where(NodeVelocities.ContainsKey).Select(number => NodeVelocities[number]).ToList();
            if (nodal.Count == 0)
                return Velocity;

            var result = new double[3];
            foreach (var velocity in nodal)
                for (var direction = 0; direction < Math.Min(3, velocity.Length); direction++)
                    result[direction] += velocity[direction] / nodal.Count;
            return result;
        }
    }

    /// <summary>
    /// Параметры контактного теплового потока по закону Нортона:
    /// q = chi * beta * K * |delta v|^(psi + 1).
    /// </summary>
    public class FrictionHeatOptions
    {
        public bool Enabled { get; set; }
        public string[] SurfaceGroups { get; set; } = [];
        public double Beta { get; set; }
        public double Consistency { get; set; }
        public double VelocityExponent { get; set; }
        public double? HeatPartition { get; set; }
        public double MaterialEffusivity { get; set; }
        public double ToolEffusivity { get; set; }
        public double[] ToolLinearVelocity { get; set; } = [0.0, 0.0, 0.0];
        public double[] ToolAngularVelocity { get; set; } = [0.0, 0.0, 0.0];
        public double[] RotationCenter { get; set; } = [0.0, 0.0, 0.0];
        public double StartTime { get; set; } = double.NegativeInfinity;
        public double StopTime { get; set; } = double.PositiveInfinity;

        internal void Validate()
        {
            if (!Enabled)
                return;
            if (SurfaceGroups == null || SurfaceGroups.Length == 0 || SurfaceGroups.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Для тепловыделения от трения задайте SurfaceGroups.");
            if (Beta < 0.0 || Consistency < 0.0 || HeatPartition is < 0.0 or > 1.0)
                throw new ArgumentOutOfRangeException(nameof(Beta), "Beta, Consistency и HeatPartition должны быть неотрицательны; HeatPartition не больше единицы.");
            if (MaterialEffusivity < 0.0 || ToolEffusivity < 0.0)
                throw new ArgumentOutOfRangeException(nameof(MaterialEffusivity), "Эффузивности должны быть неотрицательны.");
            if (VelocityExponent < 0.0)
                throw new ArgumentOutOfRangeException(nameof(VelocityExponent), "Показатель psi должен быть неотрицателен.");
            ValidateVector(ToolLinearVelocity, nameof(ToolLinearVelocity));
            ValidateVector(ToolAngularVelocity, nameof(ToolAngularVelocity));
            ValidateVector(RotationCenter, nameof(RotationCenter));
            if (StartTime > StopTime)
                throw new ArgumentException("StartTime не может быть больше StopTime.");
        }

        public double CalculateFlux(double x, double y, double z, IReadOnlyList<double> workpieceVelocity)
        {
            var rx = x - RotationCenter[0];
            var ry = y - RotationCenter[1];
            var rz = z - RotationCenter[2];
            var wx = ToolAngularVelocity[0];
            var wy = ToolAngularVelocity[1];
            var wz = ToolAngularVelocity[2];
            var toolX = ToolLinearVelocity[0] + wy * rz - wz * ry;
            var toolY = ToolLinearVelocity[1] + wz * rx - wx * rz;
            var toolZ = ToolLinearVelocity[2] + wx * ry - wy * rx;
            var dvx = toolX - GetComponent(workpieceVelocity, 0);
            var dvy = toolY - GetComponent(workpieceVelocity, 1);
            var dvz = toolZ - GetComponent(workpieceVelocity, 2);
            var relativeSpeed = Math.Sqrt(dvx * dvx + dvy * dvy + dvz * dvz);

            var partition = HeatPartition ?? (MaterialEffusivity + ToolEffusivity > 0.0 ? MaterialEffusivity / (MaterialEffusivity + ToolEffusivity) : 1.0);
            return partition * Beta * Consistency * Math.Pow(relativeSpeed, VelocityExponent + 1.0);
        }

        private static double GetComponent(IReadOnlyList<double> vector, int index) => index < vector.Count ? vector[index] : 0.0;

        private static void ValidateVector(double[] vector, string name)
        {
            if (vector == null || vector.Length != 3 || vector.Any(value => !double.IsFinite(value)))
                throw new ArgumentException($"{name} должен содержать три конечные компоненты.", name);
        }
    }
}
