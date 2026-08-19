using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using Model.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.ElementData
{
    public class EM2DAP : ElementMechanical
    {
        public EM2DAP(double thickness, Beam element) : base(element)
        {
            Thickness = thickness;
        }

        public double Thickness { get; }

        public override Vector<double> ElasticStrain_Calc(Vector<double> stress)
        {
            throw new NotImplementedException();
        }

        public override Matrix<double> El_ElasticMatrix_Calc()
        {
            throw new NotImplementedException();
        }

        public override Vector<double> Force_Calc(Vector<double> strain)
        {
            throw new NotImplementedException();
        }

        public override Vector<double> IncTermalStrain_Calc(float dtemp)
        {
            throw new NotImplementedException();
        }

        public override double IntensityStrain_Calc(Vector<double> strain)
        {
            throw new NotImplementedException();
        }

        public override double IntensityStress_Calc(Vector<double> stress)
        {
            throw new NotImplementedException();
        }

        public override Vector<double> Ball_Calc(Vector<double> stress)
        {
            throw new NotImplementedException();
        }

        public override Matrix<double> Stiffness_Calc()
        {
            throw new NotImplementedException();
        }

        public override Vector<double> Strain_Calc(Vector<double> displeNode)
        {
            throw new NotImplementedException();
        }

        public override Vector<double> Stress_Calc(Vector<double> strain)
        {
            throw new NotImplementedException();
        }

        public override Vector<double> TermalStrain_Calc()
        {
            throw new NotImplementedException();
        }
    }
}
