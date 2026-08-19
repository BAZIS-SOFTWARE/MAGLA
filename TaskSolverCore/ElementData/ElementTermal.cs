using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.ElementData
{
    public abstract class ElementTermal : ElementItem
    {
        // Делаем размерность 3 (каждый элемент в зависимости от
        // задачи будет использовать нужные элементы массива)
        public double[] HeatTransfer { get; set; } = new double[3];
        public double HeatCapacity { get; set; }
        public double Density { get; set; }
        public float HeatVelocity { get; set; }
        protected ElementTermal(IElement element) : base(element)
        {
        }

        public override string ToString()
        {
            return "Термический " + base.ToString();
        }

        /// <summary>
        /// HeatTransfer_Calc
        /// </summary>
        /// <returns></returns>
        public abstract Matrix<double> HeatTransfer_Calc();
        /// <summary>
        /// Capacity_Calc
        /// </summary>
        /// <returns></returns>
        public abstract Matrix<double> Capacity_Calc();


        /// <summary>
        /// VolumeHeat. Определение объемного тепловыделения в узлах. Тепло генерируется в центре элемента
        /// </summary>
        /// <param name="heatValue"></param>
        /// <returns></returns>
        public abstract Vector<double> VolumeHeat_Calc(double heatValue);
    }
}
