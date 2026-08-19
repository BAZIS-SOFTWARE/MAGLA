

namespace PropertiesCalculator.PropertiesController.Interfaces
{
    /// <summary>
    /// IHardeningCalculator
    /// </summary>
    public interface IHardeningModel<T>
    {
        /// <summary>
        /// Calc
        /// </summary>
        /// <param name="yield"></param>
        /// <param name="slope"></param>
        /// <param name="tensile"></param>
        /// <param name="eqEp"></param>
        /// <returns></returns>
        T Calc(T yield, T slope, T tensile, T eqEp);
    }
}