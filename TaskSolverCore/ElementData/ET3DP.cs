using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.ElementData
{
    internal class ET3DP : ElementTermal
    {
        public double Thickness { get; }
        public ET3DP(double thickness, IElement2D element) : base(element)
        {
            Thickness = thickness;
        }

        public override Matrix<double> Capacity_Calc()
        {
            throw new NotImplementedException();
        }

        public override Matrix<double> HeatTransfer_Calc()
        {
            throw new NotImplementedException();
        }

        public override Vector<double> VolumeHeat_Calc(double heatValue)
        {
            throw new NotImplementedException();
        }
    }
}
