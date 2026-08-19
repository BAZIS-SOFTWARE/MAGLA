using Model.MeshObjects;
using CAESolvers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;

namespace TaskSolverCore
{
    public abstract partial class HeatTask
    {
        public override void FillMatrices(
            MatrixContainer matr,
            ElementsData<ElementTermal> elemData,
            NodesData geo,
            float timeStep)
        {
            SymmetricCSRMatrix mK =
                matr.Get<SymmetricCSRMatrix>(MatrixType.heatTransfer);
            SymmetricCSRMatrix mC =
                matr.Get<SymmetricCSRMatrix>(MatrixType.heatCapacity);
            SymmetricCSRMatrix mKC =
                matr.Get<SymmetricCSRMatrix>(MatrixType.heatTransferCapacity);


            //var length = elemData.Count();
            foreach (var item in elemData)
            {
                //var eObj = elemData[i].Element;
                //var nbrNodes = geo.MatrixIndex[i].Count;

                var mHeatTransfer = item.HeatTransfer_Calc();
                var mCapacity = item.Capacity_Calc();

                //var cStr = mCapacity.ToString();
                //var hStr = mHeatTransfer.ToString();

                // Важно!!! необходима синхронизация между индексами узлов
                // локальной и глобальной матриц

                var gInds = geo.GetGlobalInds(item.Element, Dof);

                for (int k = 0; k < gInds.Count; k++)
                {
                    var row = gInds[k];

                    // можно перебирать не сначала, а со сдвигом по k так как
                    // храняться элементы выше диагонали и матрицы всегда симметричны
                    for (int m = 0; m < gInds.Count; m++)
                    {
                        var col = gInds[m];
                        if (col >= row)
                        {
                            mK[row, col] += mHeatTransfer[k, m];
                            mC[row, col] += mCapacity[k, m];
                        }

                        //var scol = 0;
                        //if (mK.Kind == MatrixKind.profile)
                        //    scol = mK.Indexes[row].BinarySearch(col);
                        //else
                        //    scol = col - row;
                        //mK[row, scol] = mK[row, scol] + mHeatTransfer[k, m];
                        //mC[row, scol] = mC[row, scol] + mCapacity[k, m];
                        //}
                    }
                }
            }

            ReadOnlySpan<int> rowPointers = mK.RowPointers;
            ReadOnlySpan<int> columnIndices = mK.ColumnIndices;

            for (int row = 0; row < mK.Size; row++)
            {
                for (int position = rowPointers[row];
                    position < rowPointers[row + 1];
                    position++)
                {
                    int col = columnIndices[position];
                    mKC[row, col] = mK[row, col] + mC[row, col] / timeStep;
                }
            }
        }
    }
}
