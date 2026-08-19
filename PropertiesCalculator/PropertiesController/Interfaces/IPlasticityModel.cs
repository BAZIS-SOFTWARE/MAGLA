using MathNet.Numerics.LinearAlgebra;
using System;

namespace PropertiesCalculator.PropertiesController.Interfaces
{
    public interface IPlasticityModel<T> where T : struct, IEquatable<T>, IFormattable
    {
        /// <summary>
        /// IntensityStress_Calc
        /// </summary>
        /// <param name="stress"></param>
        /// <returns></returns>
        T IntensityStress_Calc(Vector<T> stress);
        /// <summary>
        /// IntensityStrain_Calc
        /// </summary>
        /// <param name="strain"></param>
        /// <returns></returns>
        T IntensityStrain_Calc(Vector<T> strain);
    }
}
