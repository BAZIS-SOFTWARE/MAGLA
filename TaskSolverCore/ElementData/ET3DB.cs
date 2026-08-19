using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces;
using Model.Interfaces.MeshObjects;
using Model.MeshObjects;
using Mono.Unix.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IronPython.Modules.PythonIterTools;

namespace TaskSolverCore.ElementData
{
    public class ET3DB : ElementTermal
    {
        public ET3DB(double diameter, Beam element) : base(element)
        {
            Square = Math.PI * Math.Pow(diameter, 2) / 4;

            Length = element.CalcLength();
        }

        public double Square { get; }
        public float Length { get; }

        public override Matrix<double> Capacity_Calc()
        {
            var capacity = HeatCapacity * Density * Square * Length / 6;

            var mCapacity = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);

            mCapacity[0, 0] = 2 * capacity;
            mCapacity[0, 1] = capacity;
            mCapacity[1, 0] = capacity;
            mCapacity[1, 1] = 2 * capacity;

            return mCapacity;
        }

        public override Matrix<double> HeatTransfer_Calc()
        {

            var stiff = Square * HeatTransfer[0] / Length;

            var mStiff = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);

            mStiff[0, 0] = stiff;
            mStiff[0, 1] = -stiff;
            mStiff[1, 0] = -stiff;
            mStiff[1, 1] = stiff;

            return mStiff;
        }
/// <inheritdoc/>

        public override Vector<double> VolumeHeat_Calc(double heatValue)
        {
            var heatNode = heatValue * Square * Length / 2;
            return Vector<double>.Build.DenseOfArray([heatNode, heatNode]);
        }
    }
}
