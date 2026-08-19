using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskSolverCore.ElementData;

namespace TaskSolverCore.MatrixCalculator
{
    internal class Mech2DPlaneCalculator : IMechCalculator
    {
        public Vector<double> ElasticStrain_Calc(float young, Vector<double> stress)
        {
            throw new NotImplementedException();
        }

        public Matrix<double> El_ElasticMatrix_Calc(float young, float phi)
        {
            throw new NotImplementedException();
        }

        public Vector<double> Force_Calc(ElementItem elem, Vector<double> strain)
        {
            throw new NotImplementedException();
        }

        public double IntensityStrain_Calc(Vector<double> strain)
        {
            throw new NotImplementedException();
        }

        public double IntensityStress_Calc(Vector<double> stress)
        {
            throw new NotImplementedException();
        }

        public Matrix<double> Stiffness_Calc(ElementItem elem)
        {
            throw new NotImplementedException();
        }

        public Vector<double> Strain_Calc(IElement elem, Vector<double> displeNode)
        {
            throw new NotImplementedException();
        }

        public Vector<double> Stress_Calc(float young, float phi, Vector<double> strain)
        {
            throw new NotImplementedException();
        }

        public Vector<double> TermalStrain_Calc(float hExtCoeff, float temp)
        {
            throw new NotImplementedException();
        }
    }
}
