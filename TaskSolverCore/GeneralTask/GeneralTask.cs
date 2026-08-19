//using PrFunctionLib;

using CAESolvers;
using Project.Interfaces;
using Project.Interfaces.Tasks;
using System.Data;
using System.Diagnostics;
using Model.Interfaces.MeshObjects;
using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;
using TaskSolverCore.Vector;
using TaskSolverCore.MatrixSolvers;
using Project.Tasks;
using Project.TaskParameters;
using ResultDB.IO;
using ResultDB;
using MaterialDB.MaterialData;

namespace TaskSolverCore
{
    /// <summary>
    /// Tasks
    /// </summary>
    [Flags]
    public enum taskKind
    {
        /// <summary>
        /// химическая
        /// </summary>
        химическая = 2,
        /// <summary>
        /// термическая
        /// </summary>
        термическая = 4,
        /// <summary>
        /// механическая
        /// </summary>
        механическая = 8,
    }
    public abstract partial class GeneralTask
    {
        /// <summary>
        /// Degrees of freedom
        /// </summary>
        public int Dof { get; internal set; } 
        public TaskType TaskType { get; internal set; }
        public taskKind TaskKind { get; internal set; }
        public abstract void Calc();

        public Action<string> TaskInfoEvent;

        public string Folder { get; internal set; }
    }

    public abstract partial class GeneralTask<T> : GeneralTask where T : ElementItem
    {
        //ElementsData<T> ElementsData { get; }
        /// <summary>
        /// Обновление матрицы задачи
        /// </summary>
        public bool UpdMatrix { get; set; } = true;

        enum TaskStatus : int { computed, aborted, finished }

        private readonly ISymmetricLinearSolver matrixSolver;

        internal GeneralParameters Parameters;

        public GeneralTask(int index, string folder, ITaskData taskData, GeneralParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            MatData = taskData.Find<MatData>().ToList();
            Index = index;

            Folder = folder;

            ResultsLoader = new LoadResultsFileDB();
            ResultsLoader.LoadEvent += (ar1, ar2) => { WriteToLog(ar2); };

            matrixSolver = SolverBuilder.Create(parameters.SolverSettings);

            TaskType = taskData.TaskType;

            taskResults = new List<Result>();
        }

        public List<Result> taskResults;

        internal LoadResultsFileDB ResultsLoader;

        public int Index { get; }

        public SolverSettings SolverSettings { get { return Parameters.SolverSettings; } }

        public TimeSettings TimeSettings { get { return Parameters.TimeSettings; } }

        public int Iterations { get { return Parameters.Iterations; } }

        public string RestartFile { get { return Parameters.RestartFile; } }

        public Dictionary<string,double> InitialTemp { get { return Parameters.InitTemp; } }

        public List<string> MetallurgicalProcesses
        { get { return Parameters.MetallurgicalProcesses; } }

        public int SaveRate { get { return Parameters.SaveRate; } }


        private TaskStatus Status;

        internal List<MatData> MatData;

        public override void Calc()
        {
            File.Delete($@"{Folder}\ComputationData\{TaskKind}_{Index}.log");

            WriteToLog($"Матричный решатель {matrixSolver.GetType().Name}");

            WriteToLog("Создание физических элементов...");
            // хранят геометрию, получают тем-ру и фаз-ый состав
            var elemsData = CreateElementData();

            //var nodesNumbers = elemsData.GetNodesNumbers();
            //var elemsNumbers = elemsData.GetElementsNumbers();

            WriteToLog($"Использовано узлов {elemsData.GetNodesNumbers().Count}, " +
    $"элементов {elemsData.GetElementsNumbers().Count}");

            var nodesData = new NodesData(elemsData.GetNodesNumbers());

            Result iniResults;
            if (RestartFile == "")
                iniResults = CreateIntialResult(TimeSettings.StartTime, elemsData);
            else
            {
                iniResults = LoadInitialResults(RestartFile);
                WriteToLog("Приложение начальных условий...");
                elemsData.SetInitialCondition(iniResults);
            }
         
            taskResults.Add(iniResults);

            WriteToLog("Проверка нагрузок и граничных условий...");
            CheckLoadAndBoundaryConditions(nodesData, elemsData);
            WriteToLog("Проверка используемых функций...");
            //CheckFunctionsData();

            Status = TaskStatus.computed;

            var resDir = $@"{Folder}\ResultsData";

            if (!Directory.Exists(resDir))
                Directory.CreateDirectory(resDir);


            SaveResultsToDb(taskResults.Last(), false);

            var watch = new Stopwatch();
            watch.Start();
            WriteToLog("Начало расчета...");
            Solve_numeric(elemsData, nodesData);
            watch.Stop();
            var t = watch.Elapsed.TotalSeconds.ToString();

            WriteToLog(string.Format("Задача {0} {1} завершена. Время расчета {2} сек.", TaskKind, Index, t));
            //}
        }

        public void WriteToLog(string msg) => File.AppendAllText($@"{Folder}\ComputationData\{TaskKind}_{Index}.log", $"{msg} \n");  

        public abstract void ClearVectorLoads(VectorContainer<double> vec);
        public abstract void ClearVectorResults(VectorContainer<double> vec);

        /// <summary>
        /// CreateElementData
        /// </summary>
        /// <param name="matData"></param>
        /// <param name="taskType"></param>
        /// <param name="processes"></param>
        /// <param name="iniTemp"></param>
        /// <returns></returns>
        public abstract ElementsData<T> CreateElementData();
        public abstract void CheckLoadAndBoundaryConditions(NodesData geo,ElementsData<T> elementsData);
        public abstract void SaveTaskResults(VectorContainer<double> vec, ElementsData<T> mat, NodesData geo, float time);
        /// <summary>
        /// Формирование глобальных матриц задачи
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        public abstract MatrixContainer FormMatrices(NodesData nodes, IEnumerable<IElement> elements);
        public abstract VectorContainer<double> FormVectors(int numbrNodes, int numbrElems);
        public abstract void ClearMatrices(MatrixContainer matrixData);
        public abstract void SetPhysicalProp(NodesData geo, ElementsData<T> mat, float time);
        public abstract void FillMatrices(MatrixContainer matr, ElementsData<T> elemData, NodesData geo, float timeStep);
        public abstract void ApplyLoads(VectorContainer<double> vec, MatrixContainer matr, NodesData geo, ElementsData<T> mat, float time);
        public abstract void ApplyBoundCondition(VectorContainer<double> vectorData, MatrixContainer matrixData, NodesData geomData, ElementsData<T> mat, float time);
        public abstract Result CreateIntialResult(float time, ElementsData<T> elementsData);
        public abstract DataSet CreateDataSet(List<string> phasesNames);
        public abstract void ApplyPreLoads(VectorContainer<double> vec, MatrixContainer matr, NodesData geo, ElementsData<T> mat, float time, float timeStep);
        //public abstract Tuple<bool, double, float> CheckConvergence(double[] x1, double[] x0, NodesData geo, ElementsData<T> mat, float timeStep);

        public abstract bool CheckMaxDeltaResu(double dx_max);
        public abstract bool CheckMaxResu(double x_max);

        public abstract double[] CalcResidualForces(double[] x1, NodesData geo, ElementsData<T> mat, float timeStep);
    }
}
