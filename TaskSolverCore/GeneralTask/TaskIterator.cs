//using PrFunctionLib;

using Model;
using Mono.Unix.Native;
using Project.TaskParameters;
using ResultDB;
using System.Diagnostics.Metrics;
using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;
using TaskSolverCore.Vector;

namespace TaskSolverCore
{
    public abstract partial class GeneralTask<T>
    {
        private bool TaskIterator(
            ElementsData<T> elemData,
            NodesData geo,
            MatrixContainer matr,
            VectorContainer<double> vec,
            ref float timeStep,
            float time)
        {
            var converge = false;

            for (var j = 1; j < Iterations; j++)  // start iteration cycle
            {
                WriteToLog(string.Format("\n  Итерация {0}", j));
                //if (j > 1)
                //{
                    if(UpdMatrix)
                    {
                        ClearVectorLoads(vec);
                        ClearMatrices(matr);
                        WriteToLog("Построение матрицы задачи...");
                        FillMatrices(matr, elemData, geo, timeStep);
                        UpdMatrix = false;
                    }


                    WriteToLog("Приложение первоначальных нагрузок...");
                    ApplyPreLoads(vec, matr, geo, elemData, time, timeStep);
                //}
                WriteToLog("Приложение нагрузок и граничных условий...");
                ApplyLoads(vec, matr, geo, elemData, time);
                ApplyBoundCondition(vec, matr, geo, elemData, time);

                var resu = Solve_system(vec.GetVectorArray(VectorType.force), matr);

                var x1 = resu.Item1;
                var accuracy = resu.Item2;
                //vec.GetVectorList().Add(xj);

                var iteratorStatus = CheckAccuracy(accuracy);
                if (iteratorStatus)
                {
                    var x0 = vec.GetVectorArray(VectorType.result);

                    var dx_max = MatrixSolvers.Error.AbsoluteMax(x0.Vector, x1);

                    if (dx_max == -1)
                        break;

                    var x_max = x1.Max(x => Math.Abs(x));

                    // если требуется сообщить об обновлении матрицы задачи
                    if (j % Parameters.UpdMatrixIteration == 0)
                        UpdMatrix = true;
 
                    if(!CheckMaxResu(x_max))
                        iteratorStatus = false;

                    converge = CheckMaxDeltaResu(dx_max);
                    var dy = CalcResidualForces(x1, geo, elemData, timeStep);
                    //var check = CheckConvergence(x1, x0.Vector, geo, elemData, timeStep);

                    var dy_max = dy.Max();
                    // временно для проверки
                    if (dy_max > 30)
                        iteratorStatus = false;

                    vec.RemoveVectors(VectorType.result);
                    vec.AddVector(VectorType.result, new VectorArray<double>(x1));

                    WriteToLog(string.Format(" > Max x {0}, Div dx {1} dy {2}", x_max,dx_max, dy_max));
                }
                else WriteToLog(string.Format(" Итерации остановлены"));

                timeStep = CheckTimeStep(timeStep, converge, ref iteratorStatus, j);

                if (converge | !iteratorStatus)
                    break;
            }

            return converge;
        }
    }
}
