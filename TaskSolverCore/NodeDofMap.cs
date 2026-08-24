using Model.Interfaces.MeshObjects;

namespace TaskSolverCore
{
    /// <summary>
    /// Неизменяемое соответствие между номерами узлов модели и индексами
    /// глобальных степеней свободы расчетной системы.
    /// </summary>
    public sealed class NodeDofMap
    {
        private readonly int[] nodeNumbers;
        private readonly Dictionary<int, int> nodes;

        public NodeDofMap(IEnumerable<int> nodeNumbers)
        {
            this.nodeNumbers = nodeNumbers.ToArray();
            var nodeIndex = 0;
            nodes = this.nodeNumbers.ToDictionary(
                number => number,
                _ => nodeIndex++);
        }

        public IEnumerable<int> GetNodesNumbs => nodeNumbers;

        public int Count => nodes.Count;

        public bool ContainsNode(int number) =>
            nodes.ContainsKey(number);

        public int IndexOfNode(int number) =>
            nodes[number];

        public List<int> GetGlobalInds(
            IElement element,
            int degreesOfFreedom)
        {
            if (degreesOfFreedom <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(degreesOfFreedom));

            var globalIndices = new List<int>(
                element.NumberOfPoints * degreesOfFreedom);

            foreach (var node in element.GetVertexes())
            {
                var nodeIndex = IndexOfNode(node.Number);

                for (var component = 0;
                    component < degreesOfFreedom;
                    component++)
                {
                    globalIndices.Add(
                        degreesOfFreedom * nodeIndex + component);
                }
            }

            return globalIndices;
        }
    }
}
