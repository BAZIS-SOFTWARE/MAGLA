using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskSolverCore.Matrix
{
    public class MatrixIndex
    {
        List<int>[] mInd;

        public int Length { get { return mInd.Length; } }

        public int this[int row, int col]
        {
            get { return mInd[row][col]; }
            set
            {
                mInd[row][col] = value;
            }
        }

        public List<int> this[int row]
        {
            get { return mInd[row]; }
            set { mInd[row] = value; }
        }

        public int BandWidth { get; set; }
        public int Degrees { get; }

        public MatrixIndex(List<int> numbNodes, List<int> numbElems,int degrees)
        {
            Degrees = degrees;
            mInd = new List<int>[numbElems.Count];
            //order = Enumerable.Range(0, numbNodes.Count * Degrees).ToList();
        }

        public MatrixIndex(List<int>[] mInd, int size)
        {
            //Degrees = degrees;
            //this.mInd = mInd;

            var incidentMatrix = new List<int>[size];

            for (int i = 0; i < incidentMatrix.Length; i++)
            {
                incidentMatrix[i] = new List<int>();
            }

            //found a new new_NoneZeroNodes
            for (int i = 0; i < mInd.Length; i++)
            {
                foreach (var vert in mInd[i])
                {
                    foreach (var incVert in mInd[i])
                    {
                        if (!incidentMatrix[vert].Contains(incVert))
                            incidentMatrix[vert].Add(incVert);
                    }
                }
            }

            //order = Enumerable.Range(0, numbNodes.Count * Degrees).ToList();
        }
    }
}
