using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskSolverCore.ElementData;

namespace TaskSolverCore.BoundaryConditions
{
    internal class HeatBoundary2DPlane : IHeatBoundary
    {
        public Matrix<double> Capacity_Calc(ElementItem elementItem)
        {
            throw new NotImplementedException();
        }

        public Matrix<double> ExchangeBoundary_Calc(IElement elem, double heatExch)
        {
            throw new NotImplementedException();
        }

        public Vector<double> FlowBoundary_Calc(IElement elem, double mediaTemp, double heatExch)
        {
            throw new NotImplementedException();
        }

        public Vector<double> FlowHeat_Calc(IElement elem, float flowValue)
        {
            throw new NotImplementedException();
        }

        public Matrix<double> GetFormGradientMatrix(Matrix<double> dN)
        {
            throw new NotImplementedException();
        }

        public Matrix<double> HeatTransfer_Calc(ElementItem elementItem)
        {
            throw new NotImplementedException();
        }

        public Vector<double> VolumeHeat_Calc(ElementItem elementItem, double heatValue)
        {
            throw new NotImplementedException();
        }
    }
}
