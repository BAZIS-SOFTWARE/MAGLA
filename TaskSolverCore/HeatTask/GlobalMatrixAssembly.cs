using CAESolvers;
using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;

namespace TaskSolverCore
{
    public abstract partial class HeatTask
    {
        protected override void FillMatrices(TaskSystemContext<ElementTermal> context)
        {
            var matrices = context.Matrices;
            var mK = matrices.Get<CSRMatrix>(MatrixType.heatTransfer);
            var mC = matrices.Get<CSRMatrix>(MatrixType.heatCapacity);
            var mKC = matrices.Get<CSRMatrix>(MatrixType.heatTransferCapacity);

            AssembleHeatMatrices(context, mK, mC);

            if (Convection)
            {
                var mA = matrices.Get<CSRMatrix>(MatrixType.heatConvection);
                AssembleConvectionMatrix(context, mA);
                CombineMatrices(mK, mC, mA, mKC, context.TimeStep);
                return;
            }

            CombineMatrices(mK, mC, mKC, context.TimeStep);
        }

        private void AssembleHeatMatrices(TaskSystemContext<ElementTermal> context, CSRMatrix mK, CSRMatrix mC)
        {
            foreach (var element in context.Elements)
            {
                var heatTransfer = element.HeatTransfer_Calc();
                var capacity = element.Capacity_Calc();
                var globalIndices = context.Nodes.GetGlobalInds(element.Element, Dof);

                for (var row = 0; row < globalIndices.Count; row++)
                for (var column = 0; column < globalIndices.Count; column++)
                {
                    mK[globalIndices[row], globalIndices[column]] += heatTransfer[row, column];
                    mC[globalIndices[row], globalIndices[column]] += capacity[row, column];
                }
            }
        }

        private void AssembleConvectionMatrix(TaskSystemContext<ElementTermal> context, CSRMatrix mA)
        {
            foreach (var element in context.Elements)
            {
                var globalIndices = context.Nodes.GetGlobalInds(element.Element, Dof);
                ConvectionAssembler.Assemble(element, mA, globalIndices);
            }
        }

        private static void CombineMatrices(CSRMatrix mK, CSRMatrix mC, CSRMatrix mKC, double timeStep)
        {
            var rowPointers = mK.RowPointers;
            var columnIndices = mK.ColumnIndices;
            for (var row = 0; row < mK.RowCount; row++)
            for (var position = rowPointers[row]; position < rowPointers[row + 1]; position++)
            {
                var column = columnIndices[position];
                mKC[row, column] = mK[row, column] + mC[row, column] / timeStep;
            }
        }

        private static void CombineMatrices(CSRMatrix mK, CSRMatrix mC, CSRMatrix mA, CSRMatrix mKC, double timeStep)
        {
            var rowPointers = mK.RowPointers;
            var columnIndices = mK.ColumnIndices;
            for (var row = 0; row < mK.RowCount; row++)
            for (var position = rowPointers[row]; position < rowPointers[row + 1]; position++)
            {
                var column = columnIndices[position];
                mKC[row, column] = mK[row, column] + mA[row, column] + mC[row, column] / timeStep;
            }
        }
    }
}
