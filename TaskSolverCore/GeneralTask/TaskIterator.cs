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
        private bool TaskIterator(TaskSystemContext<T> context)
        {
            var converge = false;
            var timeStep = context.TimeStep;

            for (var j = 1; j < Iterations; j++)  // start iteration cycle
            {
                WriteToLog(string.Format("\n  Итерация {0}", j));
                //if (j > 1)
                //{
                    if(UpdMatrix)
                    {
                        ClearVectorLoads(context.Vectors);
                        ClearMatrices(context.Matrices);
                        WriteToLog("Построение матрицы задачи...");
                        FillMatrices(context);
                        UpdMatrix = false;
                    }


                    WriteToLog("Приложение первоначальных нагрузок...");
                    ApplyPreLoads(context);
                //}
                WriteToLog("Приложение нагрузок и граничных условий...");
                ApplyLoads(context);
                ApplyBoundaryConditions(context);

                var resu = SolveSystem(CreateLinearSystem(context));

                var x1 = resu.Item1;
                var accuracy = resu.Item2;
                //vec.GetVectorList().Add(xj);

                var iteratorStatus = CheckAccuracy(accuracy);
                if (iteratorStatus)
                {
                    var x0 = context.Vectors.GetVectorArray(VectorType.result);

                    var dx_max = MatrixSolvers.Error.AbsoluteMax(x0.Vector, x1);

                    if (dx_max == -1)
                        break;

                    var x_max = x1.Max(x => Math.Abs(x));

                    var matrixUpdateScheduled =
                        j % Parameters.UpdMatrixIteration == 0;
                    var iterationResult = EvaluateIteration(
                        context,
                        x1,
                        dx_max,
                        x_max,
                        matrixUpdateScheduled);

                    UpdMatrix = iterationResult.MatrixMustBeUpdated;
                    converge = iterationResult.Converged;
                    iteratorStatus = iterationResult.CanContinue;

                    context.Vectors.RemoveVectors(VectorType.result);
                    context.Vectors.AddVector(
                        VectorType.result,
                        new VectorArray<double>(x1));

                    WriteToLog(
                        $" > Max x {iterationResult.SolutionMaximum}, " +
                        $"Div dx {iterationResult.SolutionChange} " +
                        $"dy {iterationResult.PhysicalResidual}");
                }
                else WriteToLog(string.Format(" Итерации остановлены"));

                timeStep = CheckTimeStep(timeStep, converge, ref iteratorStatus, j);
                context.TimeStep = timeStep;

                if (converge | !iteratorStatus)
                    break;
            }

            return converge;
        }
    }
}
