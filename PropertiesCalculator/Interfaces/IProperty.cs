using PropertiesCalculator.MaterialData.MetallurgicalData;
using PropertiesCalculator.MaterialData;
using System;
using System.Data;

namespace PropertiesCalculator.Interfaces
{
    /// <summary>
    /// IProperty
    /// </summary>
    public interface IProperty
    {
        /// <summary>
        /// Name
        /// </summary>
        string Name
        {
            get;
            set;
        }
        /// <summary>
        /// X_unit
        /// </summary>

        string X_unit { get; set; }
        /// <summary>
        /// Y_unit
        /// </summary>

        string Y_unit { get; set; }

        /// <summary>
        /// Units
        /// </summary>
        string Units { get; set; }
        /// <summary>
        /// DataTable
        /// </summary>
        DataTable DataTable
        {
            get;
            set;
        }
        /// <summary>
        /// Copy
        /// </summary>
        /// <param name="copyName"></param>
        /// <returns></returns>
        Property Copy(string copyName);

        /// <summary>
        /// CalcProp for y(a,T) type of functions
        /// </summary>
        /// <param name="phaseData"></param>
        /// <param name="temp"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        float CalcProp(PhaseData phaseData, float temp);

        /// <summary>
        /// CalcProp for y(a,T) type of functions
        /// </summary>
        /// <param name="phaseData"></param>
        /// <param name="temp"></param>
        /// <returns></returns>
        float CalcProp(DataTable phaseData, float temp);

        /// <summary>
        /// CalcProp for y(x) type of functions
        /// </summary>
        /// <param name="variable"></param>
        /// function argument "x"
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        float CalcProp(float variable);
    }
}
