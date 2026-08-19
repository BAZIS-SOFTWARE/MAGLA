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
    public abstract partial class MechTask
    {
        public override void FillMatrices(
            MatrixContainer matr,
            ElementsData<ElementMechanical> elemData,
            NodesData geo,
            float timeStep)
        {
            //var mInd = geo.MatrixIndex;
            SymmetricCSRMatrix mKC =
                matr.Get<SymmetricCSRMatrix>(MatrixType.stifness);

            //var length = elemData.Count();

            foreach (var item in elemData)
            {
                //var eObj = elemData[i].Element;
                var mLocStiff = item.Stiffness_Calc();
                //var ndInds = mInd[i].Count;
                var gInds = geo.CreateGlobalInds(item.Element, Dof);

                for (int k = 0; k < gInds.Count; k++)
                {
                    var row = gInds[k];

                    for (int m = 0; m < gInds.Count; m++)
                    {
                        var col = gInds[m];

                        if (col >= row)
                            mKC[row, col] += mLocStiff[k, m];
                    }
                }
                //}
            }

        }
    }
}
