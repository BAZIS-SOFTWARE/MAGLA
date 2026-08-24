using CAESolvers;
using TaskSolverCore.ElementData;

namespace TaskSolverCore
{
    internal interface IHeatConvectionAssembler
    {
        void Assemble(ElementTermal element, CSRMatrix matrix, IReadOnlyList<int> globalIndices);
    }

    internal sealed class GalerkinHeatConvectionAssembler : IHeatConvectionAssembler
    {
        public void Assemble(ElementTermal element, CSRMatrix matrix, IReadOnlyList<int> globalIndices)
        {
            var localMatrix = element.Convection_Calc();
            for (var row = 0; row < globalIndices.Count; row++)
                for (var column = 0; column < globalIndices.Count; column++)
                    matrix[globalIndices[row], globalIndices[column]] += localMatrix[row, column];
        }
    }
}
