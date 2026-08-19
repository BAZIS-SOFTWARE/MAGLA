
using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.Attributes;
using Model.Interfaces.MeshObjects;
using TaskSolverCore.ElementData;


namespace TaskSolverCore.MatrixCalculator
{
    public class Mech3DCalculator : IMechCalculator
    {
        //public Matrix<double> Stiffness_Calc(ElementItem elementItem)
        //{
        //    var elem = elementItem.Element;
        //    var young = elementItem.Young;
        //    var phi = elementItem.Phi;

        //    var sumStiff = Matrix<double>.Build.Dense(elem.NumberOfPoints * 3, elem.NumberOfPoints * 3);
        //    var mD = El_ElasticMatrix_Calc(young, phi);
        //    foreach (var intPoint in elem.GetIntegralPoints())
        //    {
        //        var mStiff = Matrix<double>.Build.Dense(elem.NumberOfPoints * 3, elem.NumberOfPoints * 3);
        //        var dN = Matrix<double>.Build.DenseOfArray(elem.GetDerivativesFormFunctions(intPoint));
        //        var j = intPoint.Jacobi;

        //        dN = j.Inverse().Multiply(dN);
        //        var mB = GetFormGradientMatrix(dN);
        //        var vol = Math.Abs(j.Determinant()) * intPoint.Weigt;

        //        var mBt = mB.Transpose();
        //        mBt.Multiply(mD, mBt);
        //        mBt.Multiply(mB, mStiff);
        //        mStiff.Multiply(vol, mStiff);

        //        sumStiff.Add(mStiff, sumStiff);
        //    }

        //    return sumStiff;
        //}

        public Matrix<double> El_ElasticMatrix_Calc(float young, float phi)
        {
                var KK = (1 - (2 * 0.3f)) / young;

                var Ed = (phi + (2 * KK)) / (3 * phi * KK);
                var Es = (phi - KK) / (3 * phi * KK);
                var Gd = 1 / (2 * phi);

                var mProp = new double[6, 6]; // массив упругих констант           

                mProp[0, 0] = Ed; mProp[0, 1] = Es; mProp[0, 2] = Es; mProp[0, 3] = 0; mProp[0, 4] = 0; mProp[0, 5] = 0;
                mProp[1, 0] = Es; mProp[1, 1] = Ed; mProp[1, 2] = Es; mProp[1, 3] = 0; mProp[1, 4] = 0; mProp[1, 5] = 0;
                mProp[2, 0] = Es; mProp[2, 1] = Es; mProp[2, 2] = Ed; mProp[2, 3] = 0; mProp[2, 4] = 0; mProp[2, 5] = 0;
                mProp[3, 0] = 0; mProp[3, 1] = 0; mProp[3, 2] = 0; mProp[3, 3] = Gd; mProp[3, 4] = 0; mProp[3, 5] = 0;
                mProp[4, 0] = 0; mProp[4, 1] = 0; mProp[4, 2] = 0; mProp[4, 3] = 0; mProp[4, 4] = Gd; mProp[4, 5] = 0;
                mProp[5, 0] = 0; mProp[5, 1] = 0; mProp[5, 2] = 0; mProp[5, 3] = 0; mProp[5, 4] = 0; mProp[5, 5] = Gd;

                return Matrix<double>.Build.DenseOfArray(mProp);
        }

        //public Matrix<double> GetFormGradientMatrix(Matrix<double> dN)
        //{
        //    var numberOfPoints = dN.ColumnCount;
        //    var matrix = Matrix<double>.Build.Dense(6, numberOfPoints * 3);

        //    for (int m = 0; m < numberOfPoints; m++)
        //    {
        //        matrix[0, 3 * m] = dN[0, m]; matrix[0, 3 * m + 1] = 0;        matrix[0, 3 * m + 2] = 0;
        //        matrix[1, 3 * m] = 0;        matrix[1, 3 * m + 1] = dN[1, m]; matrix[1, 3 * m + 2] = 0;
        //        matrix[2, 3 * m] = 0;        matrix[2, 3 * m + 1] = 0;        matrix[2, 3 * m + 2] = dN[2, m];
        //        matrix[3, 3 * m] = dN[1, m]; matrix[3, 3 * m + 1] = dN[0, m]; matrix[3, 3 * m + 2] = 0;
        //        matrix[4, 3 * m] = dN[2, m]; matrix[4, 3 * m + 1] = 0;        matrix[4, 3 * m + 2] = dN[0, m];
        //        matrix[5, 3 * m] = 0;        matrix[5, 3 * m + 1] = dN[2, m]; matrix[5, 3 * m + 2] = dN[1, m];
        //    }


        //    return matrix;

        //}
/// <inheritdoc/>

        public Vector<double> Force_Calc(ElementItem elementItem, Vector<double> strain)
        {
            var elem = elementItem.Element;
            var young = elementItem.Young;
            var phi = elementItem.Phi;

            var mD = El_ElasticMatrix_Calc(young, phi);
            var stress = mD.Multiply(strain);

            var summForce = Vector<double>.Build.Dense(elem.NumberOfPoints * 3);

            foreach (var intPoint in elem.GetIntegralPoints())
            {
                var j = intPoint.Jacobi;
                var vol = Math.Abs(j.Determinant()) * intPoint.Weigt;
                var subStress = stress.Multiply(vol);

                var dN = Matrix<double>.Build.DenseOfArray(elem.GetDerivativesFormFunctions(intPoint));
                dN = j.Inverse().Multiply(dN);

                var mB = GetFormGradientMatrix(dN);
                var force = mB.Transpose().Multiply(subStress);
                summForce.Add(force, summForce);
            }

            return summForce;
        }

        public Vector<double> TermalStrain_Calc(float hExtCoeff, float temp)
        {
            var strT = hExtCoeff * temp;

            var etv = Vector<double>.Build.Dense(6);

            etv[0] = strT;
            etv[1] = strT;
            etv[2] = strT;

            return etv;
        }
        /// <inheritdoc/>
        [Warning("не проверен ручным счетом!")]
        public Vector<double> Strain_Calc(IElement elem, Vector<double> displeNode)
        {
            var summStrain = Vector<double>.Build.Dense(6);

            foreach (var intPoint in elem.CreateIntegrationPoints(1))
            {
                var dN = Matrix<double>.Build.DenseOfArray(elem.GetDerivativesFormFunctions(intPoint));
                var j = intPoint.Jacobi;
                dN = j.Inverse().Multiply(dN);

                var mB = GetFormGradientMatrix(dN);

                var d_strain = mB.Multiply(displeNode);
                summStrain.Add(d_strain, summStrain);
            }

            return summStrain;
        }

        private Vector<double> CalcInterpolatedValue(Vector<double> vector, double[] N)
        {
            var matrix = Matrix<double>.Build.Dense(3, N.Length * 3);

            for (int i = 0; i < N.Length; i++)
            {
                matrix[0, 3 * i] = N[i];
                matrix[1, 3 * i + 1] = N[i];
                matrix[2, 3 * i + 2] = N[i];
            }
            var str = matrix.ToString(3, 18);
            return matrix.Multiply(vector);
        }

        public Vector<double> ElasticStrain_Calc(float young, Vector<double> stress)
        {
            var strainE = Vector<double>.Build.Dense(6);

            var g = young / (2 * (1 + 0.3f));
            var kk = (1 - (2 * 0.3f)) / young;
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

        public Vector<double> Stress_Calc(float young, float phi, Vector<double> strain)
        {
            var mProp = El_ElasticMatrix_Calc(young, phi);

            var stress = mProp.Multiply(strain); //full stress

            return stress;
        }

        /// <inheritdoc/>
        public double IntensityStrain_Calc(Vector<double> strain)
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
        /// <inheritdoc/>

        public double IntensityStress_Calc(Vector<double> stress)
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


    }
}
