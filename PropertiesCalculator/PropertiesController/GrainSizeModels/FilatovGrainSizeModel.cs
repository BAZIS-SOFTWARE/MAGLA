using MaterialDB.MaterialData;
using PropertiesCalculator.PropertiesController.Interfaces;
using System;

namespace PropertiesCalculator.PropertiesController.GrainSizeModels
{

    /// <summary>
    /// Grain size for low alloy steel
    /// </summary>
    public class FilatovGrainSizeModel : IGrainSizeModel<float>
    {
        //public ChemicalData ChemicalData { get; }
        /// <summary>
        /// Activation energy
        /// </summary>
        public float E { get; set; }
        /// <summary>
        /// AustenizationTemp
        /// </summary>
        public float AustTemp { get; set; }
        /// <summary>
        /// Предэкспоненциальный множитель А
        /// </summary>
        public float A { get; set; }
        /// <summary>
        /// Временная экспонента
        /// </summary>
        public float N { get; set; }
        /// <summary>
        /// Gas constant
        /// </summary>
        public float R { get; } = 8.314f;


        float accumulatedTime = 0;

        /// <summary>
        /// CalcGrainSize
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="currentTemp"></param>
        /// <returns></returns>
        public float Calc(float currentTime, float currentTemp)
        {
            if (currentTemp >= AustTemp)
            {
                accumulatedTime += currentTime;
            }
            else accumulatedTime = 0;

            var grainSize = A * Math.Exp(-E / (R * AustTemp)) * Math.Pow(accumulatedTime, N);
            return (float)grainSize;
        }
        /// <summary>
        /// CalcActivationEnergy
        /// </summary>
        /// <param name="chemicalData"></param>
        /// <returns></returns>
        public void CalcActivationEnergy(ChemicalData chemicalData)
        {
            E = 89098 + (3581 * chemicalData[ChemElement.C]) + (1211 * chemicalData[ChemElement.Ni]) + (1443 * chemicalData[ChemElement.Cr]) +
                (4031 * chemicalData[ChemElement.Mo]);
        }
        /// <summary>
        /// CalcAustenizationTempreture
        /// </summary>
        /// <param name="chemicalData"></param>
        /// <returns></returns>
        public void CalcAustenizationTempreture(ChemicalData chemicalData)
        {
            AustTemp = (float)(910 - (203 * Math.Sqrt(chemicalData[ChemElement.C])) + (44.7 * chemicalData[ChemElement.Si]) - (15.2 * chemicalData[ChemElement.Ni])
               + (31.5 * chemicalData[ChemElement.Mo]) + (104 * chemicalData[ChemElement.V]) + (13.1 * chemicalData[ChemElement.W]) - (30 * chemicalData[ChemElement.Mn])
              + (11 * chemicalData[ChemElement.Cr]) + (20 * chemicalData[ChemElement.Cu]) - (700 * chemicalData[ChemElement.P]) - (400 * chemicalData[ChemElement.Al])
               - (120 * chemicalData[ChemElement.As]) - (400 * chemicalData[ChemElement.Ti]));
        }

        
    }
}
