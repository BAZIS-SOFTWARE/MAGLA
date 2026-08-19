
using System.Data;

namespace PropertiesCalculator.PropertiesController.Interfaces
{

    /// <summary>
    /// IMetallurgicalModel
    /// </summary>
    public interface IMetallurgicalModel
    {
        /// <summary>
        /// Calc
        /// </summary>
        /// <param name="table"></param>
        /// <param name="temp"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        float Calc(DataTable table, float temp, float time);
    }
}