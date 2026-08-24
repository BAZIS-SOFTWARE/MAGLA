
using System;

namespace Project.TaskParameters
{
    [Serializable]
    public class TermalParameters : GeneralParameters, IChemicalDependent 
    {             
        public string ChemicalFile { get; set; } = "";
        /// <summary>
        /// Доп. опция для источника конвекции (файл результатов газо/гидродинамики)
        /// </summary>
        public bool ConvectionIsFile { get; set; } = false;
        /// <summary>
        /// Файл конвекции
        /// </summary>
        public string ConvectionFile { get; set; } = "";
        /// <summary>
        /// Величина конвекции (компоненты вектора скорости: x,y,z)
        /// </summary>
        public double[] ConvectionlLoad { get; set; } = [];
        /// <summary>
        /// Флад конвекции
        /// </summary>
        public bool Convection { get; set; } = false;

        public TermalConvergence TermalConvergence { get; set; } = new TermalConvergence();

        public TermalParameters()
        {
            TaskKind = Interfaces.Tasks.TaskKind.термическая;
        }

    
    }
}
