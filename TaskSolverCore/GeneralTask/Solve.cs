//using PrFunctionLib;

using CAESolvers;
using Model;
using Project.TaskParameters;
using ResultDB;
using System;
using System.Diagnostics;
using System.Linq;
using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;
using TaskSolverCore.Vector;

namespace TaskSolverCore
{
    public abstract partial class GeneralTask<TElement, TMatrix>
    {
        private void Solve_numeric(ElementsData<TElement> elemsData, NodeDofMap nodesData)
        {
            var timeInd = 1;
            var timeStep = TimeSettings.InitTimeStep;
            var time = TimeSettings.StartTime + timeStep;

            var elems = elemsData.Select(x => x.Element);

            var matr = FormMatrices(nodesData, elems);
            var vec = FormVectors(nodesData.Count, elemsData.Count);
            var context = new TaskSystemContext<TElement>(
                elemsData, nodesData, matr, vec);

            while (time <= TimeSettings.StopTime & Status == TaskStatus.computed) // start step cycle
            {
                context.Time = time;
                context.TimeStep = timeStep;

                WriteToLog(string.Format("\t Время {0}  Шаг {1}", time, timeStep));
                WriteToLog("Определение свойств материалов...");
                SetPhysicalProperties(context);

                WriteToLog("Построение матрицы задачи...");
                FillMatrices(context);
                
                UpdMatrix = false;
                //if (timeInd == 1) Solve_simbol(matr, nodesData);

                //WriteToLog("Приложение первоначальных нагрузок...");
                //ApplyPreLoads(vec, matr, nodesData, elemsData, time, timeStep);

                var converge = TaskIterator(context);
                timeStep = context.TimeStep;

                if (converge)
                {
                    WriteToLog("-------- Сходимость достигнута --------");
                    SaveTaskResults(vec, elemsData, nodesData, time);
                    SaveProjectResults(taskResults);
                    timeInd++;
                }
                else WriteToLog("-------- Нет сходимости --------");

                CheckTime(ref time, timeStep, converge, taskResults);
                var progress = Math.Round(100.0 * (time - TimeSettings.StartTime) / (TimeSettings.StopTime - TimeSettings.StartTime), 1);
                var taskInfo = $"Время : {time.ToString("0.#####E+00")}, Шаг : {timeStep.ToString("0.#####E+00")}, Прогресс : {progress}, Этап : {timeInd}";

                TaskInfoEvent?.Invoke(taskInfo);

                WriteToLog($"{progress}%");

                ClearVectorLoads(vec);
                ClearMatrices(matr);
                ClearVectorResults(vec);
            }
        }

        //public void Solve_simbol(MatrixContainer<double> matrixData, NodeDofMap geomData)
        //{
        //    MatrixNumeric<double> mKC;

        //    if (TaskKind == taskKind.механическая)
        //        mKC = matrixData[MatrixType.stifness];
        //    else
        //    {
        //        mKC = matrixData[MatrixType.heatTransferCapacity];
        //    }


        //    if (SolverSettings.Solver == "Gauss_direct")
        //    {
        //        WriteToLog("Сивольное решение...");

        //        var solver = MatrixSolver as BandGaussDirect;
        //        var bandMatrix = mKC as BandMatrix<double>;

        //        var graph = new Graph(bandMatrix.Length);

        //        graph.ConnectVerteces(bandMatrix.R_Inds);

        //        //act

        //        var simbol = graph.MakeExclusion_2();

        //        //var teorBeand = mKC.R_Inds.Select(x => x.Count()).Max();

        //        //var simb = solver?.U_Simbol_2(mKC.Values, bandMatrix.Width);
        //        //var length = simb?.Item1.Length;

        //        bandMatrix.SetIndexes(simbol);
        //            //mKC.R_Inds[i].SetIndexes(simb.Item1[i], i);
        //    }
        //    else
        //    {
        //        // пока закоментим, так как кажется это убирает нужные связи
        //        //mKC.ReduceZeroIndexes();
        //        //mKC.ReduceZeroElements();
        //    }
        //}

        private Tuple<double[], double> SolveSystem(LinearSystem<TMatrix> system)
        {
            WriteToLog("Численное решение...");

            var watch = new Stopwatch();
            var matrix = system.Matrix;
            var rightHandSide = system.RightHandSide;

            watch.Start();

            var solution = matrixSolver.Solve(system);
            var accuracy = RelativeResidual(matrix, rightHandSide, solution);

            if (matrixSolver is ConjugateGradientGaussPreSolver conjugateGradient)
            {
                var result = conjugateGradient.LastResult!;
                WriteToLog($" > всего итераций {result.Iterations}, относительная невязка {accuracy}");
            }

            watch.Stop();

            var t = watch.Elapsed.TotalSeconds.ToString();
            WriteToLog(" > время расчета " + t + " сек.");
            return new Tuple<double[], double>(solution, accuracy);
        }

        private static double RelativeResidual(TMatrix matrix, double[] rightHandSide, double[] solution)
        {
            var product = matrix.Multiply(solution);
            var residualSquared = 0.0;
            var rightHandSideSquared = 0.0;

            for (var i = 0; i < rightHandSide.Length; i++)
            {
                var residual = rightHandSide[i] - product[i];
                residualSquared += residual * residual;
                rightHandSideSquared += rightHandSide[i] * rightHandSide[i];
            }

            var denominator = Math.Sqrt(rightHandSideSquared);
            if (denominator < 1e-300)
                denominator = 1.0;

            return Math.Sqrt(residualSquared) / denominator;
        }
    }
}
