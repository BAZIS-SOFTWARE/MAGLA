using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using TaskSolverCore.ElementData;

namespace TaskSolverCore.MatrixCalculator
{
    /*
     * TO DO изменить интерфейс калькулятора для ситуации, когда он наследуется ElementItem
     */
    public interface IMechCalculator
    {
        //Matrix<double> Stiffness_Calc(ElementItem elementItem);

        //Matrix<double> El_ElasticMatrix_Calc(float young, float phi);

        //Vector<double> Force_Calc(ElementItem elementItem, Vector<double> strain);

        //Vector<double> Strain_Calc(IElement elem, Vector<double> displeNode);

        //Vector<double> TermalStrain_Calc(float hExtCoeff, float temp);

        //Vector<double> ElasticStrain_Calc(float young, Vector<double> stress);

        //Vector<double> Stress_Calc(float young, float phi, Vector<double> strain);

        //double IntensityStrain_Calc(Vector<double> strain);

        //double IntensityStress_Calc(Vector<double> stress);
    }
}
