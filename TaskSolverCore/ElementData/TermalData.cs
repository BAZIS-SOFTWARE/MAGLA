using MaterialDB.MaterialData;
using MaterialDB.MaterialData.MetallurgicalData;
using Model.Interfaces.MeshObjects;
using Project.Interfaces.Tasks;
using Project.Tasks.Materials;
using Project.Tasks;
using ResultDB;
using System.Collections;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using TaskSolverCore.Matrix;

namespace TaskSolverCore.ElementData
{
    public class TermalData : ElementsData<ElementTermal>
    {
        public override void SetInitialCondition(Result result)
        {
            //Подумать над правильностью реализации хранения и чтения результатов
            //Сейчас температура и фазовый состав хранятся вместе с теплофизикой
            //и механическими результатами.
            foreach (var item in eItems)
            {
                //обновление температуры
                var row = result.Data.Tables["elements"].Rows.Find(item.Key);

                item.Value.Temp = (float)row["T"];
                //обновление фазового состава
                foreach (var phase in item.Value.PhaseData)
                    phase.Value = (float)row[phase.Name];
            }

        }

    }
}
