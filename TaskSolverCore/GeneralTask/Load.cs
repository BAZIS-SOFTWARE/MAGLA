//using PrFunctionLib;

using ResultDB;
using TaskSolverCore.ElementData;

namespace TaskSolverCore
{
    public abstract partial class GeneralTask<T>
    {
        private Result LoadInitialResults(string file)
        {
            var dbName = Directory.GetFiles($@"{Folder}\ResultsData", file);
            if (dbName.Length == 0)
                throw new Exception($"Отсутствует файл результатов {file}");

            var times = ResultsLoader.GetValues(dbName[0], "nodes", "Time");

            if (times.Count() == 0)
                throw new Exception($"В файле {file} отсутствуют результаты...");

            WriteToLog($"Загрузка результатов для времени {times.Last()}...");
            return ResultsLoader.GetResult(dbName[0], new List<string>() { "nodes", "elements" }, times.Last());
        }

        /// <inheritdoc/>
        public void FillElementData(Result result, ElementsData<T> elemsData)
        {
            //Подумать над правильностью реализации хранения и чтения результатов
            //Сейчас температура и фазовый состав хранятся вместе с теплофизикой
            //и механическими результатами.
            foreach (var item in elemsData)
            {
                //обновление температуры
                var row = result.Data.Tables["elements"].Rows.Find(item.Number);

                item.Temp = (float)row["T"];
                //обновление фазового состава
                foreach (var phase in item.PhaseData)
                    phase.Value = (float)row[phase.Name];
            }

        }
    }
}
