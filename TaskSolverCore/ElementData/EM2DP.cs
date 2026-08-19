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
    public class EM2DP : ElementMechanical
    {
        public EM2DP(double thickness, IElement2D element) : base(element)
        {
            Thickness = thickness;
            Tensor = Vector<double>.Build.Dense(3);
        }

        public double Thickness { get; }



        public override Vector<double> ElasticStrain_Calc(Vector<double> stress)
        {
            var strainE = Vector<double>.Build.Dense(3);

            var kk = (1 - (2 * 0.3f)) / Young;

            //var g = Young / (2 * (1 + 0.3f));
            //var phi = 1 / (2 * g); // phi under room temperature                 

            var sx = stress[0];
            var sy = stress[1];
            var sxy = stress[2];


            var meanS = (sx + sy) / 3;

            strainE[0] = ((sx - meanS) * Phi) + (meanS * kk);
            strainE[1] = ((sy - meanS) * Phi) + (meanS * kk);
            strainE[2] = 2 * Phi * sxy;

            return strainE;
        }

        public override Matrix<double> El_ElasticMatrix_Calc()
        {
            var KK = (1 - (2 * 0.3f)) / Young;

            var Ed = (2 * Phi + KK) / (Phi * (Phi + 2 * KK));
            var Es = (Phi - KK) / (Phi * (Phi + 2 * KK));
            var Gd = 1 / (2 * Phi);

            var mProp = new double[3, 3]; // массив упругих констант           

            mProp[0, 0] = Ed;   mProp[0, 1] = Es;   mProp[0, 2] = 0;
            mProp[1, 0] = Es;   mProp[1, 1] = Ed;   mProp[1, 2] = 0;
            mProp[2, 0] = 0;    mProp[2, 1] = 0;    mProp[2, 2] = Gd;

            return Matrix<double>.Build.DenseOfArray(mProp);
        }

        public override Vector<double> Force_Calc(Vector<double> strain)
        {
            var mD = El_ElasticMatrix_Calc();

            var stress = mD.Multiply(strain);

            var summForce = Vector<double>.Build.Dense(Element.NumberOfPoints * 2);

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

        public override Vector<double> IncTermalStrain_Calc(float dtemp)
        {
            var etv = Vector<double>.Build.Dense(3);
            etv[0] = HeatExpCoeff[0] * dtemp;
            etv[1] = HeatExpCoeff[1] * dtemp;

            return etv;
        }

        public override double IntensityStrain_Calc(Vector<double> strain)
        {
            var mean = (strain[0] - strain[1]) / 3;
            var ex = strain[0] - mean;
            var ey = strain[1] - mean;
            var exy = strain[2];

            var tangS = 3.0/4 * Math.Pow(exy, 2);
            var normS = Math.Pow(ex, 2) + Math.Pow(ey, 2) - ex * ey;
            var misS = Math.Sqrt(2.0/3 * (normS + tangS)); //  stress intensity  
            return misS;
        }

        public override double IntensityStress_Calc(Vector<double> stress)
        {
            var mean = (stress[0] + stress[1]) / 3;
            var sx = stress[0] - mean;
            var sy = stress[1] - mean;
            var sxy = stress[2];

            var tangS = 3 * Math.Pow(sxy, 2);
            var normS = Math.Pow(sx, 2) + Math.Pow(sy, 2) - sx * sy;
            var misS =  Math.Sqrt(normS + tangS); //  stress intensity  
            return misS;
        }

        public override Matrix<double> Stiffness_Calc()
        {
            var sumStiff = Matrix<double>.Build.Dense(Element.NumberOfPoints * 2, Element.NumberOfPoints * 2);
            var mD = El_ElasticMatrix_Calc();
            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var mStiff = Matrix<double>.Build.Dense(Element.NumberOfPoints * 2, Element.NumberOfPoints * 2);
                var dN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                var j = intPoint.Jacobi;

                dN = j.Inverse().Multiply(dN);
                var mB = GetFormGradientMatrix(dN);
                var vol = Math.Abs(j.Determinant()) * intPoint.Weigt * Thickness;

                var mBt = mB.Transpose();
                mBt.Multiply(mD, mBt);
                mBt.Multiply(mB, mStiff);
                mStiff.Multiply(vol, mStiff);

                sumStiff.Add(mStiff, sumStiff);
            }

            return sumStiff;
        }

        public Matrix<double> GetFormGradientMatrix(Matrix<double> dN)
        {
            var numberOfPoints = dN.ColumnCount;
            var matrix = Matrix<double>.Build.Dense(3, numberOfPoints * 2);

            for (int m = 0; m < numberOfPoints; m++)
            {
                //b_sub[m] = new double[4, 2];
                matrix[0, 2 * m] = dN[0, m];    matrix[0, 2 * m + 1] = 0;
                matrix[1, 2 * m] = 0;           matrix[1, 2 * m + 1] = dN[1, m];
                matrix[2, 2 * m] = dN[1, m];    matrix[2, 2 * m + 1] = dN[0, m];
            }


            return matrix;

        }

        public override Vector<double> Strain_Calc(Vector<double> displeNode)
        {
            var summStrain = Vector<double>.Build.Dense(3);

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

        public override Vector<double> TermalStrain_Calc()
        {
            var etv = Vector<double>.Build.Dense(3);
            etv[0] = HeatExpCoeff[0] * Temp;
            etv[1] = HeatExpCoeff[1] * Temp;

            return etv;
        }

        public override Vector<double> Ball_Calc(Vector<double> tensor)
        {
            var ball = Vector<double>.Build.Dense(3);

            var mean = (tensor[0] + tensor[1]) / 3;
            ball[0] = mean;
            ball[1] = mean;

            return ball;
        }
    }
}
