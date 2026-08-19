using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.Attributes;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskSolverCore.ElementData;

namespace TaskSolverCore.MatrixCalculator
{
    /// <summary>
    /// Mech2DAxiCalculator_v2. Использовать с осторожностью. Класс лучше проходит базовые тесты чем его аналог, но на больших моделях
    /// аналог ведет себя лучше
    /// </summary>
    [Warning("В этой версии квадратура используется только для расчета объема")]
    public class Mech2DAxiCalculator_v2 : IMechCalculator
    {
        public Matrix<double> FormGradientMatrix_Calc(IElement elem)
        {
            var x_Coords = elem.GetVertexes().Select(x => x.Position._x);

            var intPoint = elem.CreateIntegrationPoints(1).First();

            var j = intPoint.Jacobi;

            var N = elem.GetFormFunctions(intPoint);
            var r = CalcInterpolatedValue(x_Coords, N); //вычислим растояние до каждой точки интегрирования в цикле

            var dN = Matrix<double>.Build.DenseOfArray(elem.GetDerivativesFormFunctions(intPoint));
            dN = j.Inverse().Multiply(dN);

            return GetFormGradientMatrix(dN, N, r);

        }

        public Matrix<double> GetFormGradientMatrix(Matrix<double> dN, double[] N, double rm)
        {
            var numberOfPoints = dN.ColumnCount;

            var matrix = Matrix<double>.Build.Dense(4, numberOfPoints * 2);

            for (int m = 0; m < numberOfPoints; m++)
            {

                matrix[0, 2 * m] = dN[0, m];  matrix[0, 2 * m + 1] = 0;
                matrix[1, 2 * m] = 0;         matrix[1, 2 * m + 1] = dN[1, m];
                matrix[2, 2 * m] = N[m] / rm; matrix[2, 2 * m + 1] = 0;
                matrix[3, 2 * m] = dN[1, m];  matrix[3, 2 * m + 1] = dN[0, m];
            }

            return matrix;
        }

        private double CalcInterpolatedValue(IEnumerable<float> values, double[] N)
        {
            var counter = 0;
            var intpValue = 0.0;
            foreach (var value in values)
                intpValue += value * N[counter++];
            return intpValue;
        }


        public Vector<double> ElasticStrain_Calc(float young, Vector<double> stress)
        {
            var strainE = Vector<double>.Build.Dense(4);

            var g = young / (2 * (1 + 0.3f));
            var kk = (1 - (2 * 0.3f)) / young;
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

        public Matrix<double> El_ElasticMatrix_Calc(float young, float phi)
        {
            var KK = (1 - (2 * 0.3f)) / young;

            var Ed = (phi + (2 * KK)) / (3 * phi * KK);
            var Es = (phi - KK) / (3 * phi * KK);
            var Gd = 1 / (2 * phi);

            var mProp = new double[4, 4]; // массив упругих констант           

            mProp[0, 0] = Ed; mProp[0, 1] = Es; mProp[0, 2] = Es; mProp[0, 3] = 0;
            mProp[1, 0] = Es; mProp[1, 1] = Ed; mProp[1, 2] = Es; mProp[1, 3] = 0;
            mProp[2, 0] = Es; mProp[2, 1] = Es; mProp[2, 2] = Ed; mProp[2, 3] = 0;
            mProp[3, 0] = 0; mProp[3, 1] = 0; mProp[3, 2] = 0; mProp[3, 3] = Gd;

            return Matrix<double>.Build.DenseOfArray(mProp);
        }

        public Vector<double> Force_Calc(ElementItem elemItem, Vector<double> strain)
        {
            var elem = elemItem.Element;
            var young = elemItem.Young;
            var phi = elemItem.Phi;

            var mD = El_ElasticMatrix_Calc(young, phi);
            var stress = mD.Multiply(strain);

            var x_Coords = elem.GetVertexes().Select(x => x.Position._x);

            var vol = 0.0;
            foreach (var intPoint in elem.GetIntegralPoints())
            {
                var j = intPoint.Jacobi;
                var sqr = Math.Abs(j.Determinant()) * intPoint.Weigt;
                // теплопроводность Вт/(мм*С)(изотропная)
                //var multRR = (heatTransfer[eInd] * 2 * 3.14f * rm * rm) / (4 * sqr);
                var N = elem.GetFormFunctions(intPoint);
                var r = CalcInterpolatedValue(x_Coords, N); //вычислим растояние до каждой точки интегрирования в цикле

                vol += 2 * 3.14f * r * sqr;
            }

            var mB = FormGradientMatrix_Calc(elem);

            var subStress = stress.Multiply(vol);
            return mB.Transpose().Multiply(subStress);
        }

        public double IntensityStrain_Calc(Vector<double> strain)
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

        public double IntensityStress_Calc(Vector<double> stress)
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

        public Matrix<double> Stiffness_Calc(ElementItem elemItem)
        {
            var elem = elemItem.Element;
            var young = elemItem.Young;
            var phi = elemItem.Phi;

            var x_Coords = elem.GetVertexes().Select(x => x.Position._x);
            var mD = El_ElasticMatrix_Calc(young, phi);
            var mStiff = Matrix<double>.Build.Dense(elem.NumberOfPoints * 2, elem.NumberOfPoints * 2);
            var mB = FormGradientMatrix_Calc(elem);
            var vol = 0.0;
            foreach (var intPoint in elem.GetIntegralPoints())
            {
                var N = elem.GetFormFunctions(intPoint);
                var r = CalcInterpolatedValue(x_Coords, N); //вычислим растояние до каждой точки интегрирования в цикле

                var j = intPoint.Jacobi;
                var sqr = Math.Abs(j.Determinant()) * intPoint.Weigt;
                vol += 2 * 3.14f * r * sqr;
            }

            var mBt = mB.Transpose();
            mBt.Multiply(mD, mBt);
            mBt.Multiply(mB, mStiff);
            return mStiff.Multiply(vol);
        }

        public Vector<double> Strain_Calc(IElement elem, Vector<double> displeNode)
        {
            var strain = Vector<double>.Build.Dense(4);

            var mB = FormGradientMatrix_Calc(elem);
            mB.Multiply(displeNode, strain);

            return strain;
        }

        public Vector<double> Stress_Calc(float young, float phi, Vector<double> strain)
        {
            var mProp = El_ElasticMatrix_Calc(young, phi);
            var stress = mProp.Multiply(strain); //full stress
            return stress;
        }

        public Vector<double> TermalStrain_Calc(float hExtCoeff, float temp)
        {
            var strT = hExtCoeff * temp;

            var etv = Vector<double>.Build.Dense(4);
            etv[0] = strT;
            etv[1] = strT;
            etv[2] = strT;
            return etv;
        }
    }
}
