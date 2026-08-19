using MaterialDB.Utilities;
using System;
using System.Data;
using System.Linq;

namespace PropertiesCalculator.PropertiesCalculator.MetallurgicalModels
{
    public class Kostinen
    {
        /// <summary>
        /// Calc
        /// </summary>
        /// <param name="table"></param>
        /// <param name="temp"></param>
        /// <returns></returns>
        public float Calc(DataTable table, float temp)
        {
            var phaseAr = table.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[1])).ToArray();
            var tempAr = table.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[0])).ToArray();

            return InterpolationSearch.InterpolatedValue(tempAr, phaseAr, temp);
        }
    }
}
