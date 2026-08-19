using MaterialDB.MaterialData.MetallurgicalData;
using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces;
using Model.Interfaces.MeshObjects;
using Model.MeshObjects;
using PropertiesCalculator.PropertiesController.Interfaces;
using PropertiesCalculator.PropertiesController.MechanicalModels;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TaskSolverCore.ElementData
{
    public abstract class ElementItem
    {
        /// <summary>
        /// ElementItem
        /// </summary>
        /// <param name="element"></param>
        public ElementItem(IElement element)
        {
            Element = element;      
        }
        public int Number { get { return Element.Number; }}
        public IElement Element { get; internal set; }
        public string Material { get; set; }
        /// <summary>
        /// Temp of element
        /// </summary>
        public float Temp { get; set; }
        public int Status { get; set; } = 0;

        /// <summary>
        /// FusionTemp
        /// </summary>
        public float FusionTemp { get; set; }

        public PhaseData PhaseData { get; set; }

        public ProcessData ProcessData { get; set; }

/// <inheritdoc/>

        public override string ToString()
        {
            return $"Элемент : {Element}, Материал : {Material}, " +
                $"Температура : {Temp}, Состав : {PhaseData}, Статус : {Status}";
        }


    }
}
