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
    public abstract partial class GeneralTask<T>
    {
        private void Solve_numeric(ElementsData<T> elemsData, NodesData nodesData)
        {
            var timeInd = 1;
            var timeStep = TimeSettings.InitTimeStep;
            var time = TimeSettings.StartTime + timeStep;

            WriteToLog("Перенумерация матрицы задачи...");

            var elems = elemsData.Select(x => x.Element);
            
            var bandWidth = nodesData.MakeRenumbering(elems);
            WriteToLog($"Ширина полосы {bandWidth}");

            var matr = FormMatrices(nodesData, elems);
            var vec = FormVectors(nodesData.Count, elemsData.Count);

            while (time <= TimeSettings.StopTime & Status == TaskStatus.computed) // start step cycle
            {
                WriteToLog(string.Format("\t Время {0}  Шаг {1}", time, timeStep));
                WriteToLog("Определение свойств материалов...");
                SetPhysicalProp(nodesData, elemsData, time);

                WriteToLog("Построение матрицы задачи...");
                FillMatrices(matr, elemsData, nodesData, timeStep);
                
                UpdMatrix = false;
                //if (timeInd == 1) Solve_simbol(matr, nodesData);

                //WriteToLog("Приложение первоначальных нагрузок...");
                //ApplyPreLoads(vec, matr, nodesData, elemsData, time, timeStep);

                var converge = TaskIterator(elemsData, nodesData, matr, vec, ref timeStep, time);

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

        //public void Solve_simbol(MatrixContainer<double> matrixData, NodesData geomData)
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

        public Tuple<double[], double> Solve_system(VectorArray<double> y, MatrixContainer matrixes)
        {
            WriteToLog("Численное решение...");

            var watch = new Stopwatch();

            SymmetricCSRMatrix matrix;
            if (TaskKind == taskKind.механическая)
                matrix = matrixes.Get<SymmetricCSRMatrix>(MatrixType.stifness);
            else
                matrix = matrixes.Get<SymmetricCSRMatrix>(MatrixType.heatTransferCapacity);

            double[] rightHandSide = y.Vector.ToArray();

            watch.Start();

            double[] solution;
            double accuracy;

            if (matrixSolver is ConjugateGradientGaussPreSolver conjugateGradient)
            {
                solution = conjugateGradient.Solve(matrix, rightHandSide);
                var result = conjugateGradient.LastResult!;

                accuracy = RelativeResidual(matrix, rightHandSide, solution);

                WriteToLog(
                    $" > всего итераций {result.Iterations}, " +
                    $"относительная невязка {accuracy}");
            }
            else
            {
                solution = matrixSolver.Solve(matrix, rightHandSide);
                accuracy = RelativeResidual(matrix, rightHandSide, solution);
            }

            watch.Stop();

            var t = watch.Elapsed.TotalSeconds.ToString();
            WriteToLog(" > время расчета " + t + " сек.");
            return new Tuple<double[], double>(solution, accuracy);
        }

        private static double RelativeResidual(
            SymmetricCSRMatrix matrix, double[] rightHandSide, double[] solution)
        {
            double[] product = matrix.Multiply(solution);
            double residualSquared = 0.0;
            double rightHandSideSquared = 0.0;

            for (int i = 0; i < rightHandSide.Length; i++)
            {
                double residual = rightHandSide[i] - product[i];
                residualSquared += residual * residual;
                rightHandSideSquared += rightHandSide[i] * rightHandSide[i];
            }

            double denominator = Math.Sqrt(rightHandSideSquared);
            if (denominator < 1e-300)
                denominator = 1.0;

            return Math.Sqrt(residualSquared) / denominator;
        }
    }
}
