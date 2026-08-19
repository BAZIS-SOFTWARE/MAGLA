using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using ResultDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IronPython.Modules.PythonIterTools;

namespace TaskSolverCore.ElementData
{
    public class EM3DV : ElementMechanical
    {
        public EM3DV(IElement3D element) : base(element)
        {
            Tensor = Vector<double>.Build.Dense(6);
        }

        public override Vector<double> Force_Calc(Vector<double> strain)
        {
            var mD = El_ElasticMatrix_Calc();

            var stress = mD.Multiply(strain);

            var summForce = Vector<double>.Build.Dense(Element.NumberOfPoints * 3);

            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var j = intPoint.Jacobi;
                var vol = Math.Abs(j.Determinant()) * intPoint.Weigt;
                var subStress = stress.Multiply(vol);

                var dN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                dN = j.Inverse().Multiply(dN);

                var mB = GetFormGradientMatrix(dN);
                var force = mB.Transpose().Multiply(subStress);
                summForce.Add(force, summForce);
            }

            return summForce;
        }

        public override Matrix<double> El_ElasticMatrix_Calc()
        {
            var KK = (1 - (2 * 0.3f)) / Young;

            var Ed = (Phi + (2 * KK)) / (3 * Phi * KK);
            var Es = (Phi - KK) / (3 * Phi * KK);
            var Gd = 1 / (2 * Phi);

            var mProp = new double[6, 6]; // массив упругих констант           

            mProp[0, 0] = Ed; mProp[0, 1] = Es; mProp[0, 2] = Es; mProp[0, 3] = 0; mProp[0, 4] = 0; mProp[0, 5] = 0;
            mProp[1, 0] = Es; mProp[1, 1] = Ed; mProp[1, 2] = Es; mProp[1, 3] = 0; mProp[1, 4] = 0; mProp[1, 5] = 0;
            mProp[2, 0] = Es; mProp[2, 1] = Es; mProp[2, 2] = Ed; mProp[2, 3] = 0; mProp[2, 4] = 0; mProp[2, 5] = 0;
            mProp[3, 0] = 0; mProp[3, 1] = 0; mProp[3, 2] = 0; mProp[3, 3] = Gd; mProp[3, 4] = 0; mProp[3, 5] = 0;
            mProp[4, 0] = 0; mProp[4, 1] = 0; mProp[4, 2] = 0; mProp[4, 3] = 0; mProp[4, 4] = Gd; mProp[4, 5] = 0;
            mProp[5, 0] = 0; mProp[5, 1] = 0; mProp[5, 2] = 0; mProp[5, 3] = 0; mProp[5, 4] = 0; mProp[5, 5] = Gd;

            return Matrix<double>.Build.DenseOfArray(mProp);
        }

        public Matrix<double> GetFormGradientMatrix(Matrix<double> dN)
        {
            var numberOfPoints = dN.ColumnCount;
            var matrix = Matrix<double>.Build.Dense(6, numberOfPoints * 3);

            for (int m = 0; m < numberOfPoints; m++)
            {
                matrix[0, 3 * m] = dN[0, m]; matrix[0, 3 * m + 1] = 0; matrix[0, 3 * m + 2] = 0;
                matrix[1, 3 * m] = 0; matrix[1, 3 * m + 1] = dN[1, m]; matrix[1, 3 * m + 2] = 0;
                matrix[2, 3 * m] = 0; matrix[2, 3 * m + 1] = 0; matrix[2, 3 * m + 2] = dN[2, m];
                matrix[3, 3 * m] = dN[1, m]; matrix[3, 3 * m + 1] = dN[0, m]; matrix[3, 3 * m + 2] = 0;
                matrix[4, 3 * m] = dN[2, m]; matrix[4, 3 * m + 1] = 0; matrix[4, 3 * m + 2] = dN[0, m];
                matrix[5, 3 * m] = 0; matrix[5, 3 * m + 1] = dN[2, m]; matrix[5, 3 * m + 2] = dN[1, m];
            }


            return matrix;

        }

        public override Matrix<double> Stiffness_Calc()
        {
            //var elem = elementItem.Element;
            //var young = elementItem.Young;
            //var phi = elementItem.Phi;

            var sumStiff = Matrix<double>.Build.Dense(Element.NumberOfPoints * 3, Element.NumberOfPoints * 3);
            var mD = El_ElasticMatrix_Calc();
            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var mStiff = Matrix<double>.Build.Dense(Element.NumberOfPoints * 3, Element.NumberOfPoints * 3);
                var dN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                var j = intPoint.Jacobi;

                dN = j.Inverse().Multiply(dN);
                var mB = GetFormGradientMatrix(dN);
                var vol = Math.Abs(j.Determinant()) * intPoint.Weigt;

                var mBt = mB.Transpose();
                mBt.Multiply(mD, mBt);
                mBt.Multiply(mB, mStiff);
                mStiff.Multiply(vol, mStiff);

                sumStiff.Add(mStiff, sumStiff);
            }

            return sumStiff;
        }

        /// <inheritdoc/>
        public override Vector<double> TermalStrain_Calc()
        {
            var etv = Vector<double>.Build.Dense(6);

            etv[0] = HeatExpCoeff[0] * Temp;
            etv[1] = HeatExpCoeff[1] * Temp;
            etv[2] = HeatExpCoeff[2] * Temp;

            return etv;
        }
        /// <inheritdoc/>
        public override Vector<double> IncTermalStrain_Calc(float dtemp)
        {
            var etv = Vector<double>.Build.Dense(6);

            etv[0] = HeatExpCoeff[0] * dtemp;
            etv[1] = HeatExpCoeff[1] * dtemp;
            etv[2] = HeatExpCoeff[2] * dtemp;

            return etv;
        }

        /// <inheritdoc/>
        public override Vector<double> ElasticStrain_Calc(Vector<double> stress)
        {
            var strainE = Vector<double>.Build.Dense(6);

            var kk = (1 - (2 * 0.3f)) / Young;

            var g = Young / (2 * (1 + 0.3f));
            var phi = 1 / (2 * g); // phi under room temperature                 

            var sx = stress[0];
            var sy = stress[1];
            var sz = stress[2];
            var sxy = stress[3];
            var sxz = stress[4];
            var syz = stress[5];

            var meanS = (sx + sy + sz) / 3;

            strainE[0] = ((sx - meanS) * phi) + (meanS * kk);
            strainE[1] = ((sy - meanS) * phi) + (meanS * kk);
            strainE[2] = ((sz - meanS) * phi) + (meanS * kk);
            strainE[3] = 2 * phi * sxy;
            strainE[4] = 2 * phi * sxz;
            strainE[5] = 2 * phi * syz;

            return strainE;
        }

        public override double IntensityStrain_Calc(Vector<double> strain)
        {
            var ex = strain[0];
            var ey = strain[1];
            var ez = strain[2];
            var exy = strain[3];
            var exz = strain[4];
            var eyz = strain[5];

            var tangE = 1.5f * (Math.Pow(exy, 2) + Math.Pow(exz, 2) + Math.Pow(eyz, 2));
            var normE = Math.Pow((ex - ey), 2) + Math.Pow((ey - ez), 2) + Math.Pow((ez - ex), 2);
            var misE = (Math.Sqrt(2) / 3) * Math.Sqrt(normE + tangE); // deformation intensity
            return misE;
        }

        public override double IntensityStress_Calc(Vector<double> stress)
        {
            var sx = stress[0];
            var sy = stress[1];
            var sz = stress[2];
            var sxy = stress[3];
            var sxz = stress[4];
            var syz = stress[5];

            var tangS = 6 * (Math.Pow(sxy, 2) + Math.Pow(sxz, 2) + Math.Pow(syz, 2));
            var normS = Math.Pow((sx - sy), 2) + Math.Pow((sy - sz), 2) + Math.Pow((sz - sx), 2);
            var misS = (1 / Math.Sqrt(2)) * Math.Sqrt(normS + tangS); //  stress intensity  
            return misS;
        }

        public override Vector<double> Strain_Calc(Vector<double> displeNode)
        {
            var summStrain = Vector<double>.Build.Dense(6);

            foreach (var intPoint in Element.CreateIntegrationPoints(1))
            {
                var dN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                var j = intPoint.Jacobi;
                dN = j.Inverse().Multiply(dN);

                var mB = GetFormGradientMatrix(dN);

                var d_strain = mB.Multiply(displeNode);
                summStrain.Add(d_strain, summStrain);
            }

            return summStrain;
        }

        public override Vector<double> Stress_Calc(Vector<double> strain)
        {
            var mProp = El_ElasticMatrix_Calc();

            var stress = mProp.Multiply(strain); //full stress

            return stress;
        }

        public override Vector<double> Ball_Calc(Vector<double> tensor)
        {
            var mean = (tensor[0] + tensor[1] + tensor[2]) / 3;
            var ball = Vector<double>.Build.
    Dense(new double[] { mean, mean, mean, 0, 0, 0 });
            return ball;
        }
    }
}
