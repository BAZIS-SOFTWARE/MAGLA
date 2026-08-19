//using PrFunctionLib;

using System.Globalization;
using Project.TaskParameters;
using ResultDB.IO;
using ResultDB;

namespace TaskSolverCore
{
    public abstract partial class GeneralTask<T>
    {
        private void SaveProjectResults(List<Result> taskResults)
        {
            var count = taskResults.Count();

            if (count % SaveRate == 0 || taskResults.Last().Time == TimeSettings.StopTime)
            {
                SaveResultsToDb(taskResults.Last(), true);
            }

            if (count - 1 == SaveRate)
            {
                var temp = taskResults.Last();
                taskResults.Clear();
                taskResults.Add(new Result(temp.Data, temp.Time, TaskKind.ToString())); // удаление всех предыдущих резултатов кроме текущего
            }
        }

        public void SaveResultsToDb(Result result, bool createFlag)
        {
            var dbName = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}_{3}.db", result.Name, Index, TimeSettings.StartTime, TimeSettings.StopTime);

            var saver = new SaveResultsFileDb();

            saver.Save(new List<Result>() { result }, $@"{Folder}\ResultsData\{dbName}", createFlag);
        }
    }
}
