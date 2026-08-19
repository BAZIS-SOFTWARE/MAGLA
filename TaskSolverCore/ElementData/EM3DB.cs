using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using Model.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TaskSolverCore.ElementData
{
    public class EM3DB : ElementMechanical
    {
        public EM3DB(double diameter, Beam element) : base(element)
        {
            Square = Math.PI * Math.Pow(diameter, 2) / 4;
            
            Length = element.CalcLength();

            //матрица преобразования в 3d пространство
            var lx = Element[1].Position._x - Element[0].Position._x;
            var ly = Element[1].Position._y - Element[0].Position._y;
            var lz = Element[1].Position._z - Element[0].Position._z;

            TransitionMatrix = GetTransitionMatrix(lx, ly, lz);
        }
        /// <summary>
        /// Square
        /// </summary>
        public double Square { get; }
        /// <summary>
        /// Length
        /// </summary>
        public double Length { get; }

        Matrix<double> TransitionMatrix { get; }
        /// <inheritdoc/>
        public override Vector<double> ElasticStrain_Calc(Vector<double> stress)
        {
            var strainE = stress.Divide(Young);
            return strainE;
        }
        /// <inheritdoc/>
        public override Matrix<double> El_ElasticMatrix_Calc()
        {
            throw new NotImplementedException();
        }
/// <inheritdoc/>

        public override Vector<double> Force_Calc(Vector<double> strainD)
        {
            var stress = strainD[0] * Young;// * strainD[0];

            var tat = TransitionMatrix.Transpose();

            var vol = Square * Length;

            var vBt = Vector<double>.Build.Dense(2);

            vBt[0] = 1 / Length;
            vBt[1] = -1 / Length;

            var force = tat.Multiply(vBt.Multiply(stress * vol));

            return force;
        }

        /// <inheritdoc/>
        public override double IntensityStrain_Calc(Vector<double> strain)
        {
            return Math.Abs(strain[0]);
        }
        /// <inheritdoc/>
        public override double IntensityStress_Calc(Vector<double> stress)
        {
            return Math.Abs(stress[0]);
        }

        private Matrix<double> GetTransitionMatrix(float lx, float ly, float lz)
        {
            // считаем что стержни у нас двух узловые
            var ta = Matrix<double>.Build.Dense(2, 6);

            var hx = lx / Length;
            var hy = ly / Length;
            var hz = lz / Length;

            ta[0, 0] = hx;
            ta[0, 1] = hy;
            ta[0, 2] = hz;
            ta[1, 3] = hx;
            ta[1, 4] = hy;
            ta[1, 5] = hz;

            return ta;
        }
/// <inheritdoc/>

        public override Matrix<double> Stiffness_Calc()
        {
            //матрица преобразования в 3d пространство
            //var lx = Element[1].Position._x - Element[0].Position._x;
            //var ly = Element[1].Position._y - Element[0].Position._y;
            //var lz = Element[1].Position._z - Element[0].Position._z;
            //var l = Math.Sqrt(lx * lx + ly * ly + lz * lz);
            //var lxy = Math.Sqrt(l * l - lz * lz);
            var ta = TransitionMatrix;
            var tat = TransitionMatrix.Transpose();

            var stiff = Young * Square / Length;

            var mStiff = Matrix<double>.Build.Dense(Element.NumberOfPoints, Element.NumberOfPoints);

            mStiff[0, 0] = stiff;
            mStiff[0, 1] = -stiff;
            mStiff[1, 0] = -stiff;
            mStiff[1, 1] = stiff;

            return tat.Multiply(mStiff).Multiply(ta);
        }
        /// <inheritdoc/>
        public override Vector<double> Strain_Calc(Vector<double> displeNode)
        {

            // B u = e
            var mB = Matrix<double>.Build.Dense(1, 2);
            
            mB[0,0] = 1 / Length;
            mB[0,1] = -1 / Length;

            var strain = mB.Multiply(TransitionMatrix.Multiply(displeNode));

            //матрица преобразования в 3d пространство
    //        var lx = Element[1].Position._x - Element[0].Position._x;
    //        var ly = Element[1].Position._y - Element[0].Position._y;
    //        var lz = Element[1].Position._z - Element[0].Position._z;
    //        var l = Math.Sqrt(lx * lx + ly * ly + lz * lz);

            

    //        var displ_f = Math.Sqrt(displeNode[0] * displeNode[0]
    //            + displeNode[1] * displeNode[1]
    //            + displeNode[2] * displeNode[2]);
    //        var displ_s = Math.Sqrt(displeNode[3] * displeNode[3]
    //+ displeNode[4] * displeNode[4]
    //+ displeNode[5] * displeNode[5]);

    //        var strain_d = (displ_s - displ_f) / l;

            var strains = Vector<double>.Build.Dense(6);
            strains[0] = strain[0];
            return strains;
        }
        /// <inheritdoc/>
        public override Vector<double> Stress_Calc(Vector<double> strain)
        {
            return strain.Multiply(Young);
        }
        /// <inheritdoc/>
        public override Vector<double> TermalStrain_Calc()
        {
            var etv = Vector<double>.Build.Dense(6);
            etv[0] = HeatExpCoeff[0] * Temp;
            return etv;
        }

        /// <inheritdoc/>
        public override Vector<double> IncTermalStrain_Calc(float dtemp)
        {
            var etv = Vector<double>.Build.Dense(6);
            etv[0] = HeatExpCoeff[0] * dtemp;
            return etv;
        }

        public override Vector<double> Ball_Calc(Vector<double> stress)
        {
            throw new NotImplementedException();
        }
    }
}
