using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IronPython.Modules.PythonIterTools;

namespace TaskSolverCore.ElementData
{
    public class EM2DAV : ElementMechanical
    {
        public EM2DAV(IElement2D element) : base(element)
        {
        }
/// <inheritdoc/>

        public override Vector<double> Force_Calc(Vector<double> strain)
        {
            //var elem = elementItem.Element;
            //var young = elementItem.Young;
            //var phi = elementItem.Phi;

            var mD = El_ElasticMatrix_Calc();
            var stress = mD.Multiply(strain);

            var summForce = Vector<double>.Build.Dense(Element.NumberOfPoints * 2);

            var x_Coords = Element.GetVertexes().Select(x => x.Position._x);

            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var j = intPoint.Jacobi;
                var sqr = Math.Abs(j.Determinant()) * intPoint.Weigt;
                // теплопроводность Вт/(мм*С)(изотропная)
                //var multRR = (heatTransfer[eInd] * 2 * 3.14f * rm * rm) / (4 * sqr);
                var N = Element.GetFormFunctions(intPoint);
                var r = CalcInterpolatedValue(x_Coords, N); //вычислим растояние до каждой точки интегрирования в цикле

                var vol = 2 * 3.14f * r * sqr;

                var dN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));
                dN = j.Inverse().Multiply(dN);

                var mB = GetFormGradientMatrix(dN, N, r);

                var subStress = stress.Multiply(vol);
                var force = mB.Transpose().Multiply(subStress);
                summForce.Add(force, summForce);
            }
            return summForce;
        }
        /// <inheritdoc/>
        public override Vector<double> ElasticStrain_Calc(Vector<double> stress)
        {
            var strainE = Vector<double>.Build.Dense(4);

            var g = Young / (2 * (1 + 0.3f));
            var kk = (1 - (2 * 0.3f)) / Young;
            var phi = 1 / (2 * g); // phi under room temperature                 

            var sx = stress[0];
            var sy = stress[1];
            var sz = stress[2];
            var sxy = stress[3];

            var meanS = (sx + sy + sz) / 3;

            strainE[0] = ((sx - meanS) * phi) + (meanS * kk);
            strainE[1] = ((sy - meanS) * phi) + (meanS * kk);
            strainE[2] = ((sz - meanS) * phi) + (meanS * kk);
            strainE[3] = 2 * phi * sxy;

            return strainE;
        }
        /// <inheritdoc/>
        public override Matrix<double> El_ElasticMatrix_Calc()
        {
            var KK = (1 - (2 * 0.3f)) / Young;

            var Ed = (Phi + (2 * KK)) / (3 * Phi * KK);
            var Es = (Phi - KK) / (3 * Phi * KK);
            var Gd = 1 / (2 * Phi);

            var mProp = new double[4, 4]; // массив упругих констант           

            mProp[0, 0] = Ed; mProp[0, 1] = Es; mProp[0, 2] = Es; mProp[0, 3] = 0;
            mProp[1, 0] = Es; mProp[1, 1] = Ed; mProp[1, 2] = Es; mProp[1, 3] = 0;
            mProp[2, 0] = Es; mProp[2, 1] = Es; mProp[2, 2] = Ed; mProp[2, 3] = 0;
            mProp[3, 0] = 0; mProp[3, 1] = 0; mProp[3, 2] = 0; mProp[3, 3] = Gd;

            return Matrix<double>.Build.DenseOfArray(mProp);
        }
        /// <inheritdoc/>
        public override Matrix<double> Stiffness_Calc()
        {
            var sumStiff = Matrix<double>.Build.Dense(Element.NumberOfPoints * 2, Element.NumberOfPoints * 2);

            var x_Coords = Element.GetVertexes().Select(x => x.Position._x);
            var mD = El_ElasticMatrix_Calc();

            foreach (var intPoint in Element.GetIntegralPoints())
            {
                var mStiff = Matrix<double>.Build.Dense(Element.NumberOfPoints * 2, Element.NumberOfPoints * 2);
                var dN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));

                var N = Element.GetFormFunctions(intPoint);
                var r = CalcInterpolatedValue(x_Coords, N); //вычислим растояние до каждой точки интегрирования в цикле

                var j = intPoint.Jacobi;
                dN = j.Inverse().Multiply(dN);

                var mB = GetFormGradientMatrix(dN, N, r);

                var sqr = Math.Abs(j.Determinant()) * intPoint.Weigt;

                // теплопроводность Вт/(мм*С)(изотропная)
                //var multRR = (heatTransfer[eInd] * 2 * 3.14f * rm * rm) / (4 * sqr);
                var vol = 2 * 3.14f * r * sqr;

                var mBt = mB.Transpose();
                mBt.Multiply(mD, mBt);
                mBt.Multiply(mB, mStiff);
                mStiff.Multiply(vol, mStiff);

                sumStiff = sumStiff.Add(mStiff);
            }
            return sumStiff;
        }
        /// <summary>
        /// CalcCentralRadius
        /// </summary>
        /// <returns></returns>
        public float CalcCentralRadius()
        {
            var enCoords = Element.GetVertexes().Select(x => x.Position._x);
            return enCoords.Sum() / Element.NumberOfPoints;
        }
        /// <summary>
        /// GetFormGradientMatrix
        /// </summary>
        /// <param name="dN"></param>
        /// <param name="N"></param>
        /// <param name="rm"></param>
        /// <returns></returns>
        public Matrix<double> GetFormGradientMatrix(Matrix<double> dN, double[] N, double rm)
        {
            var numberOfPoints = dN.ColumnCount;
            //var b_sub = new double[numberOfPoints][,];

            //var s = 1 / (2 * triangle.GetSquare(eInd));
            var matrix = Matrix<double>.Build.Dense(4, numberOfPoints * 2);

            for (int m = 0; m < numberOfPoints; m++)
            {
                //b_sub[m] = new double[4, 2];
                matrix[0, 2 * m] = dN[0, m]; matrix[0, 2 * m + 1] = 0;
                matrix[1, 2 * m] = 0; matrix[1, 2 * m + 1] = dN[1, m];
                matrix[2, 2 * m] = N[m] / rm; matrix[2, 2 * m + 1] = 0;
                matrix[3, 2 * m] = dN[1, m]; matrix[3, 2 * m + 1] = dN[0, m];
            }

            return matrix;
            //return Matrix<double>.Build.DenseOfArray(b_sub[0]).
            //Append(Matrix<double>.Build.DenseOfArray(b_sub[1])).
            //Append(Matrix<double>.Build.DenseOfArray(b_sub[2]));
        }

        private double CalcInterpolatedValue(IEnumerable<float> values, double[] N)
        {
            var counter = 0;
            var intpValue = 0.0;
            foreach (var value in values)
                intpValue += value * N[counter++];
            return intpValue;
        }
/// <inheritdoc/>

        public override Vector<double> Strain_Calc(Vector<double> displeNode)
        {
            var summStrain = Vector<double>.Build.Dense(4);

            var x_Coords = Element.GetVertexes().Select(x => x.Position._x);

            foreach (var intPoint in Element.CreateIntegrationPoints(1))
            {
                var dN = Matrix<double>.Build.DenseOfArray(Element.GetDerivativesFormFunctions(intPoint));

                var j = intPoint.Jacobi;
                dN = j.Inverse().Multiply(dN);

                var N = Element.GetFormFunctions(intPoint);
                var r = CalcInterpolatedValue(x_Coords, N); //вычислим растояние до каждой точки интегрирования

                var mB = GetFormGradientMatrix(dN, N, r);
                var strain = mB.Multiply(displeNode);
                summStrain.Add(strain, summStrain);
            }

            return summStrain;

        }
        /// <inheritdoc/>
        public override Vector<double> Stress_Calc(Vector<double> strain)
        {
            var mD = El_ElasticMatrix_Calc();
            var stress = mD.Multiply(strain); //full stress
            return stress;
        }
        /// <inheritdoc/>
        public override Vector<double> TermalStrain_Calc()
        {
            var etv = Vector<double>.Build.Dense(4);
            etv[0] = HeatExpCoeff[0] * Temp;
            etv[1] = HeatExpCoeff[1] * Temp;
            etv[2] = HeatExpCoeff[2] * Temp;
            return etv;
        }
        /// <inheritdoc/>
        public override Vector<double> IncTermalStrain_Calc(float dtemp)
        {
            var etv = Vector<double>.Build.Dense(4);
            etv[0] = HeatExpCoeff[0] * dtemp;
            etv[1] = HeatExpCoeff[1] * dtemp;
            etv[2] = HeatExpCoeff[2] * dtemp;
            return etv;
        }
        /// <inheritdoc/>
        public override double IntensityStrain_Calc(Vector<double> strain)
        {
            var ex = strain[0];
            var ey = strain[1];
            var ez = strain[2];
            var exy = strain[3];

            var tangE = 1.5f * (Math.Pow(exy, 2));
            var normE = Math.Pow((ex - ey), 2) + Math.Pow((ey - ez), 2) + Math.Pow((ez - ex), 2);
            var misE = (Math.Sqrt(2) / 3) * Math.Sqrt(normE + tangE); // deformation intensity
            return misE;
        }
        /// <inheritdoc/>
        public override double IntensityStress_Calc(Vector<double> stress)
        {
            var sx = stress[0];
            var sy = stress[1];
            var sz = stress[2];
            var sxy = stress[3];

            var tangS = 6 * (Math.Pow(sxy, 2));
            var normS = Math.Pow((sx - sy), 2) + Math.Pow((sy - sz), 2) + Math.Pow((sz - sx), 2);
            var misS = (1 / Math.Sqrt(2)) * Math.Sqrt(normS + tangS); //  stress intensity  
            return misS;
        }

        public override Vector<double> Ball_Calc(Vector<double> stress)
        {
            throw new NotImplementedException();
        }
    }
}
