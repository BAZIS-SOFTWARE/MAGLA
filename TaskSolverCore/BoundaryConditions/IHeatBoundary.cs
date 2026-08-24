using MathNet.Numerics.LinearAlgebra;
using Model.Interfaces.MeshObjects;
using TaskSolverCore.ElementData;

namespace TaskSolverCore.BoundaryConditions
{
    public interface IHeatBoundary
    {
        /// <summary>
        /// FlowBoundary_Calc. Вычисление теплового потока в среду
        /// </summary>
        /// <param name="element"></param>
        /// <param name="mediaTemp"></param>
        /// <param name="heatExch"></param>
        /// <returns></returns>
        Vector<double> FlowBoundary_Calc(IElement element, double mediaTemp, double heatExch);
        /// <summary>
        /// FlowHeat_Calc
        /// </summary>
        /// <param name="element"></param>
        /// <param name="flowValue"></param>
        /// <returns></returns>
        Vector<double> FlowHeat_Calc(IElement element, float flowValue);
        Vector<double> FlowHeat_Calc(IElement element, Func<double, double, double, double> flowValue);
        /// <summary>
        /// VolumeHeat. Определение объемного тепловыделения в узлах. Тепло генерируется в центре элемента
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="heatValue"></param>
        /// <returns></returns>
        //Vector<double> VolumeHeat_Calc(ElementItem elementItem, double heatValue);
        /// <summary>
        /// ExchangeBoundary_Calc. теплоотдача в среду
        /// </summary>
        /// <param name="element"></param>
        /// <param name="heatExch"></param>
        /// <returns></returns>
        Matrix<double> ExchangeBoundary_Calc(IElement element, double heatExch);
    }
}
