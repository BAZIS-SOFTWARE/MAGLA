using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.PropertiesController.Interfaces
{
    /// <summary>
    /// IGrainSizeModel
    /// </summary>
    public interface IGrainSizeModel<T>
    {
        /// <summary>
        /// Calc
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="currentTemp"></param>
        /// <returns></returns>
        T Calc(T currentTime, T currentTemp);
    }
}
