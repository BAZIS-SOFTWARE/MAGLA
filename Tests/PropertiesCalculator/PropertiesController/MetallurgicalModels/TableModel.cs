using MaterialDB.Utilities;
using PropertiesCalculator.PropertiesController.Interfaces;
using System;
using System.Data;
using System.Linq;

namespace PropertiesCalculator.MetallurgicalModels
{
    /// <summary>
    /// PhaseCalcJMAKModel, deprecated
    /// </summary>
    public class TableModel : IMetallurgicalModel
    {
        public float Calc(DataTable table, float temp, float time)
        {
            var p = 0.0f;

            if (table.Columns.Count == 2)
            {
                var phaseAr = table.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[1])).ToArray();
                var tempAr = table.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[0])).ToArray();

                p = InterpolationSearch.InterpolatedValue(tempAr, phaseAr,  temp);
            }

            else
            {
                var phaseAr = table.Columns.Cast<DataColumn>().Skip(1).Select(c => Convert.ToSingle(c.ColumnName.Split('_')[1])).ToArray();
                for (int i = 1; i < table.Rows.Count - 1; i++)
                {

                    var temp1 = Convert.ToSingle(table.Rows[i][0]);
                    var temp2 = Convert.ToSingle(table.Rows[i + 1][0]);

                    if (temp >= temp1 & temp <= temp2)
                    {
                        var time1Ar = table.Rows[i].ItemArray.Select(x => Convert.ToSingle(x)).Skip(1).ToArray();
                        var time2Ar = table.Rows[i + 1].ItemArray.Select(x => Convert.ToSingle(x)).Skip(1).ToArray();

                        var phase1 = InterpolationSearch.InterpolatedValue(time1Ar,phaseAr, time);
                        var phase2 = InterpolationSearch.InterpolatedValue(time2Ar,phaseAr, time);
                        //var funx = fun1 * (vel / vel1) + fun2 * (vel2 - vel / vel1);
                        var funx = InterpolationSearch.InterpolatedValue(new float[] { temp1, temp2 }, new float[] { phase1, phase2 }, temp);
                        p = funx;
                        break;
                    }
                }
                
            }

            return p;
        }
    }
}
