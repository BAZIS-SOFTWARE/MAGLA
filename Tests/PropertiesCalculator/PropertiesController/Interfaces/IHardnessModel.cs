using MaterialDB.MaterialData.MetallurgicalData;

namespace PropertiesCalculator.PropertiesController.Interfaces
{
    /// <summary>
    /// IHardnessCalculator
    /// </summary>
    public interface IHardnessModel
    {
        /// <summary>
        /// Calc
        /// </summary>
        /// <returns></returns>
        float Calc(PhaseData PhaseData);

        /// <summary>
        /// PhaseData
        /// </summary>
        //PhaseData PhaseData { get; set; }

    }
}