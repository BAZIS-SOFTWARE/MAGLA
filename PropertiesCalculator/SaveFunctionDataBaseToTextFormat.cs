using MaterialDB.Interfaces;
using System;
using System.Data;
using System.IO;

namespace PropertiesCalculator
{
    /// <summary>
    /// SaveFunctionDataBaseToTextFormat
    /// </summary>
    public class SaveFunctionDataBaseToTextFormat : ISaver
    {
        /// <inheritdoc/>
        public void SaveDataBase(DataSet data, string path)
        {
            using (var functionFile = new StreamWriter(path, false))
            {
                functionFile.WriteLine("Список");
                foreach (DataTable table in data.Tables)
                {
                    functionFile.WriteLine(table.TableName);
                }
                functionFile.WriteLine("#Список\n");

                foreach (DataTable table in data.Tables)
                {
                    functionFile.WriteLine(table.TableName);
                    for (int i = 0; i < table.Rows.Count; i++)
                    {
                        functionFile.WriteLine(Convert.ToDecimal(table.Rows[i].ItemArray[0]) + " " + Convert.ToDecimal(table.Rows[i].ItemArray[1]));
                    }
                    functionFile.WriteLine("#" + table.TableName.Split(',')[0]);
                }
            }                
        }
        
    }
}
