using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualBasic;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskSolverCore.ElementData;
using static IronPython.Modules.PythonIterTools;

namespace TaskSolverCore.MatrixCalculator
{
    public class Mech1DCalculator : IMechCalculator
    {
        public Vector<double> ElasticStrain_Calc(float young, Vector<double> stress)
        {
            throw new NotImplementedException();
        }

        public Matrix<double> El_ElasticMatrix_Calc(float young, float phi)
        {
            throw new NotImplementedException();
        }

        public Vector<double> Force_Calc(ElementItem elementItem, Vector<double> strain)
        {
            var elem = elementItem.Element;
            var young = elementItem.Young;
            var phi = elementItem.Phi;

            var stress = strain.Multiply(young);

            //матрица преобразования в 3d пространство
            var lx = elem[1].Position._x - elem[0].Position._x;
            var ly = elem[1].Position._y - elem[0].Position._y;
            var lz = elem[1].Position._z - elem[0].Position._z;
            var l = Math.Sqrt(lx * lx + ly * ly + lz * lz);
            var lxy = Math.Sqrt(l * l - lz * lz);

            var force = young * elementItem.GeometryParameter * strain[0];

            var ta = TransposeMatrix(lx, ly, lz, l);
            var tat = ta.Transpose();

            var vForces = Vector<double>.Build.Dense(elem.NumberOfPoints);
            vForces[0] = force;
            vForces[1] = force;

            return tat.Multiply(vForces);
        }

        public double IntensityStrain_Calc(Vector<double> strain)
        {
            throw new NotImplementedException();
        }

        public double IntensityStress_Calc(Vector<double> stress)
        {
            throw new NotImplementedException();
        }

        public Matrix<double> Stiffness_Calc(ElementItem elementItem)
        {
            var elem = elementItem.Element;
            var young = elementItem.Young;
            var phi = elementItem.Phi;

            //матрица преобразования в 3d пространство
            var lx = elem[1].Position._x - elem[0].Position._x;
            var ly = elem[1].Position._y - elem[0].Position._y;
            var lz = elem[1].Position._z - elem[0].Position._z;
            var l = Math.Sqrt(lx * lx + ly * ly + lz * lz);
            //var lxy = Math.Sqrt(l * l - lz * lz);
            var ta = TransposeMatrix(lx, ly, lz, l);
            var tat = ta.Transpose();

            var stiff = young * elementItem.GeometryParameter / l;

            var mStiff = Matrix<double>.Build.Dense(elem.NumberOfPoints, elem.NumberOfPoints);

            var str = ta.ToString();
            var strt = tat.ToString();

            mStiff[0, 0] = stiff;
            mStiff[0, 1] = -stiff;
            mStiff[1, 0] = -stiff;
            mStiff[1, 1] = stiff;

            return tat.Multiply(mStiff).Multiply(ta);
        }

        private static Matrix<double> TransposeMatrix(float lx, float ly, float lz, double l)
        {
            // считаем что стержни у нас двух узловые
            var ta = Matrix<double>.Build.Dense(2, 6);

            var hx = lx / l;
            var hy = ly / l;
            var hz = lz / l;

            ta[0, 0] = hx;
            ta[0, 1] = hy;
            ta[0, 2] = hz;
            ta[1, 3] = hx;
            ta[1, 4] = hy;
            ta[1, 5] = hz;

            return ta;
        }

        public Vector<double> Strain_Calc(IElement elem, Vector<double> displeNode)
        {
            //матрица преобразования в 3d пространство
            var lx = elem[1].Position._x - elem[0].Position._x;
            var ly = elem[1].Position._y - elem[0].Position._y;
            var lz = elem[1].Position._z - elem[0].Position._z;
            var l = Math.Sqrt(lx * lx + ly * ly + lz * lz);

            var displ_f = Math.Sqrt(displeNode[0] * displeNode[0]
                + displeNode[1] * displeNode[1]
                + displeNode[2] * displeNode[2]);
            var displ_s = Math.Sqrt(displeNode[3] * displeNode[3]
    + displeNode[4] * displeNode[4]
    + displeNode[5] * displeNode[5]);

            var strain = (displ_s - displ_f) / l;

            return Vector<double>.Build.Dense(1).Add(strain);
        }

        public Vector<double> Stress_Calc(float young, float phi, Vector<double> strain)
        {
            throw new NotImplementedException();
        }

        public Vector<double> TermalStrain_Calc(float hExtCoeff, float temp)
        {
            var strT = hExtCoeff * temp;

            var etv = Vector<double>.Build.Dense(1);
            etv[0] = strT;
            return etv;
        }
    }
}
