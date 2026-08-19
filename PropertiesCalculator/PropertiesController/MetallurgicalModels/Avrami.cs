using MaterialDB.Utilities;
using System;
using System.Data;
using System.Linq;

namespace PropertiesCalculator.PropertiesCalculator.MetallurgicalModels
{
    public  class Avrami
    {
        public float Calc(DataTable table, float temp,float phEx, float phCr)
        {
            var phaseCrVel = 0.0;

            var rowCount = table.Rows.Count;
            var colCount = table.Columns.Count;

            var phaseAr = table.Columns.Cast<DataColumn>().Skip(1).Select(c => Convert.ToSingle(c.ColumnName.Split('_')[1])).ToArray();

            var phaseMin = phaseAr[0];
            var phaseMax = phaseAr[colCount - 2];

            for (int i = 0; i < rowCount - 1; i++)
            {

                var temp1 = Convert.ToSingle(table.Rows[i][0]);
                var temp2 = Convert.ToSingle(table.Rows[i + 1][0]);

                if (temp >= temp1 & temp <= temp2)
                {
                    var time1_001 = Convert.ToSingle(table.Rows[i][1]);
                    var time2_001 = Convert.ToSingle(table.Rows[i + 1][1]);


                    var ts = InterpolationSearch.InterpolatedValueTwoPoints(temp1, temp2, time1_001, time2_001, temp);

                    var time1_1 = Convert.ToSingle(table.Rows[i][colCount - 1]);
                    var time2_1 = Convert.ToSingle(table.Rows[i + 1][colCount - 1]);

                    var tf = InterpolationSearch.InterpolatedValueTwoPoints(temp1, temp2, time1_1, time2_1, temp);

                    var n = Math.Log(Math.Log(1 - phaseMin) / Math.Log(1 - phaseMax)) /
                        Math.Log(Math.Log(ts) / Math.Log(tf));

                    var b = - (Math.Log(1 - phaseMin) / Math.Pow(ts,n));
                    if (b == 0)
                        break;

                    var pSumm = phEx + phCr;
                    var phDelta = phCr / pSumm;
                    var nDelta = (n - 1) / n;

                    var a = Math.Log(1 - phDelta) / b;

                    phaseCrVel = (pSumm - phCr) * b * n * Math.Pow(-a, nDelta);
                    break;
                }
            }


            return (float)phaseCrVel;
        }
    }
}
