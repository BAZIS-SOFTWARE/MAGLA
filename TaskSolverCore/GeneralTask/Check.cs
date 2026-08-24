//using PrFunctionLib;

using Model.Interfaces;
using Model.Interfaces.MeshObjects;
using TaskSolverCore.ElementData;
using TaskSolverCore.Vector;
using Project.Tasks;
using Project.TaskParameters;
using ResultDB;

namespace TaskSolverCore
{
    public abstract partial class GeneralTask<T>
    {
        public void CheckLoadData(NodeDofMap geo, ElementsData<T> elemsData, IEnumerable<CondData> objs)
        {
            foreach (var dataItem in objs)
            {
                if (dataItem.Group.ObjType == ObjType.Узел)
                {
                    if (!dataItem.Group.Any(y => geo.ContainsNode(y.Number)))
                        throw new Exception($"Не все объекты группы {dataItem.Group.Name} " +
                            $"входят в список узлов, участвующих в расчете");
                }
                else
                {
                    if (!dataItem.Group.Any(y => elemsData.ContainsElement(y.Number)))
                        throw new Exception($"Не все объекты группы {dataItem.Group.Name} " +
                            $"входят в список элементов, участвующих в расчете");
                }
            }
        }

        public void CheckBoundaryData(NodeDofMap geo, IEnumerable<CondData> objs)
        {
            foreach (var dataItem in objs)
            {
                if (dataItem.Group.ObjType == ObjType.Элемент1D | dataItem.Group.ObjType == ObjType.Элемент2D)
                {
                    foreach (var item in dataItem.Group)
                    {
                        foreach (var node in (item as IElement).GetVertexes())
                        {
                            if (!geo.ContainsNode(node.Number))
                                throw new Exception($"Узел {node.Number} элемента {item.Number} группы {dataItem.Group.Name} " +
           $"не входит в список узлов, участвующих в расчете");
                        }

                    }

                }
            }
        }
        //private bool CheckConvergence(ElementsData<T> mat, NodeDofMap geo, List<Result> taskResults, VectorContainer<double> vec, float time, bool iterStatus, int j)
        //{
        //    var convergence = false;

        //    if (iterStatus)
        //    {
        //        var check = CheckConvergence(vec, geo, mat, j, time, taskResults);
        //        convergence = check.Item1;
        //        var dx = check.Item2;
        //        var dy = check.Item3;

        //        WriteToLog(string.Format(" > Невязка dx {0} dy {1}", dx, dy));
        //    }
        //    else WriteToLog(string.Format(" Итерации остановлены"));

        //    return convergence;
        //}

        private float CheckTimeStep(float curTimeStep, bool convergence, ref bool iterStatus, int iterator)
        {
            var nextTimeStep = curTimeStep;
            if (convergence) // check precision -> continue or stop iteration procedure
            {
                nextTimeStep = curTimeStep * 1.25f;
                if (nextTimeStep > TimeSettings.MaxTimeStep)
                    nextTimeStep = TimeSettings.MaxTimeStep;
            }
            else if (!iterStatus)
            {
                nextTimeStep = curTimeStep / 1.5f;
            }
            else if (iterator + 1 == Iterations)
            {
                nextTimeStep = curTimeStep / 1.5f;
                iterStatus = false;
            }

            if (nextTimeStep < TimeSettings.MinTimeStep)
            {
                Status = TaskStatus.aborted;
            }

            return nextTimeStep;
        }

        private void CheckTime(ref float time, float timeStep, bool convergence, List<Result> taskResults)
        {
            if (convergence)
            {
                if (time.Equals(TimeSettings.StopTime))
                    Status = TaskStatus.finished;
                time += timeStep; // next time  
            }
            else
            {
                time = taskResults.Last().Time + timeStep;
            }

            if (time > TimeSettings.StopTime)
                time = TimeSettings.StopTime;
        }



        private bool CheckAccuracy(double accuracy)
        {
            if (SolverSettings.Precision != 0)
                if (accuracy.CompareTo(SolverSettings.Precision) > 0)
                {
                    return false;
                }
                else if (accuracy == -1)
                {
                    WriteToLog(string.Format("В решении есть Nan или Infinity"));

                    return false;
                }
            return true;
        }
    }
}
