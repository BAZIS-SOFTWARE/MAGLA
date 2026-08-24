using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;
using TaskSolverCore.Vector;

namespace TaskSolverCore
{
    /// <summary>
    /// Данные расчетной системы, общие для одного временного шага задачи.
    /// </summary>
    public sealed class TaskSystemContext<TElement>
        where TElement : ElementItem
    {
        public TaskSystemContext(
            ElementsData<TElement> elements,
            NodeDofMap nodes,
            MatrixContainer matrices,
            VectorContainer<double> vectors)
        {
            Elements = elements;
            Nodes = nodes;
            Matrices = matrices;
            Vectors = vectors;
        }

        public ElementsData<TElement> Elements { get; }

        public NodeDofMap Nodes { get; }

        public MatrixContainer Matrices { get; }

        public VectorContainer<double> Vectors { get; }

        public float Time { get; internal set; }

        public float TimeStep { get; internal set; }
    }
}
