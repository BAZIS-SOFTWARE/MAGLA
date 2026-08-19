using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.ElementData
{
    public abstract class ElementMechanical : ElementItem
    {
        public Vector<double> Tensor;

        // Делаем размерность 3 (каждый элемент в зависимости от
        // задачи будет использовать нужные элементы массива)
        public float[] HeatExpCoeff { get; set; } = new float[3];
        public float Relax { get; set; }
        public double Young { get; set; }
        public double Phi { get; set; }
        public float Slope { get; set; }
        public float Yield { get; set; }
        public float Tensile { get; set; }



        protected ElementMechanical(IElement element) : base(element)
        {
        }
/// <inheritdoc/>

        public override string ToString()
        {
            return  "Механический " + base.ToString();
        }

        /// <summary>
        /// Stiffness_Calc
        /// </summary>
        /// <param name="mD"></param>
        /// <returns></returns>
        public abstract Matrix<double> Stiffness_Calc();

        /// <summary>
        /// Force_Calc
        /// </summary>
        /// <param name="strain"></param>
        /// <returns></returns>
        public abstract Vector<double> Force_Calc(Vector<double> strain);
        /// <summary>
        /// Strain_Calc
        /// </summary>
        /// <returns></returns>
        public abstract Vector<double> Strain_Calc(Vector<double> displeNode);
        /// <summary>
        /// TermalStrain_Calc
        /// </summary>
        /// <returns></returns>
        public abstract Vector<double> TermalStrain_Calc();
        /// <summary>
        /// TermalStrain_Calc
        /// </summary>
        /// <param name="dtemp">Приращение температуры</param>
        /// <returns></returns>
        public abstract Vector<double> IncTermalStrain_Calc(float dtemp);
        /// <summary>
        /// ElasticStrain_Calc
        /// </summary>
        /// <param name="stress"></param>
        /// <returns></returns>
        public abstract Vector<double> ElasticStrain_Calc(Vector<double> stress);
        /// <summary>
        /// El_ElasticMatrix_Calc
        /// </summary>
        /// <returns></returns>
        public abstract Matrix<double> El_ElasticMatrix_Calc();
        /// <summary>
        /// Stress_Calc
        /// </summary>
        /// <param name="strain"></param>
        /// <returns></returns>
        public abstract Vector<double> Stress_Calc(Vector<double> strain);
        /// <summary>
        /// IntensityStrain_Calc
        /// </summary>
        /// <param name="strain"></param>
        /// <returns></returns>
        public abstract double IntensityStrain_Calc(Vector<double> strain);
        /// <summary>
        /// IntensityStress_Calc
        /// </summary>
        /// <param name="stress"></param>
        /// <returns></returns>
        public abstract double IntensityStress_Calc(Vector<double> stress);
        /// <summary>
        /// CalcPlasticStrains
        /// </summary>
        /// <param name="tensor"></param>
        /// <returns></returns>
        public Vector<double> Deviator_Calc(Vector<double> tensor)
        {
            var ball = Ball_Calc(tensor);
            return tensor.Subtract(ball);
        }
        public abstract Vector<double> Ball_Calc(Vector<double> tensor);
    }
}
