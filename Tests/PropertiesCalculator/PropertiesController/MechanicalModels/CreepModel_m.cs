using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.PropertiesController.MechanicalModels
{
    public class CreepModel_m
    {
        float relaxTime = 0;
        public float CreepStrain_Calc(float timeStep, float dEp, float relaxCoeff, float young)
        {
            if (relaxCoeff != 0)
                if (dEp > 1e-4f)
                {
                    relaxTime = 0;
                    return 0;
                }

                else
                {
                    relaxTime = relaxTime + timeStep;
                    return 1 - (float)Math.Exp(-(relaxTime * relaxCoeff) / young);
                }
            else return 0;
        }
    }
}
