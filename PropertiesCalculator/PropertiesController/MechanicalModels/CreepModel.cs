using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.PropertiesController.MechanicalModels
{
    public class CreepModel
    {

        float relaxTime = 0;
        public Vector<float> CreepStrain_Calc(float timeStep, float dEp, float relaxCoeff,float young, Vector<float> strainE)
        {
            if (relaxCoeff != 0)
                if (dEp > 1e-4f)
                {
                    relaxTime = 0;
                    return Vector<float>.Build.Dense(6);
                }

                else
                {
                    var strainDev = Vector<float>.Build.Dense(6);
                    var strainMean = (strainE[0] + strainE[1] + strainE[2]) / 3;
                    strainDev[0] = strainE[0] - strainMean;
                    strainDev[1] = strainE[1] - strainMean;
                    strainDev[2] = strainE[2] - strainMean;
                    strainDev[3] = strainE[3];
                    strainDev[4] = strainE[4];
                    strainDev[5] = strainE[5];

                    relaxTime = relaxTime + timeStep;
                    var psi = (float)Math.Exp(-(relaxTime * relaxCoeff) / young);
                    //var psi = (materials[eInd].RelaxTime * materials[eInd].RelaxCoeff) / materials[eInd].Young;
                    var strainC = strainDev.Multiply(1 - psi);
                    return strainC;
                }
            else return Vector<float>.Build.Dense(6);
        }
    }
}
