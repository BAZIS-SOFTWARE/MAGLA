using PropertiesCalculator.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace PropertiesCalculator
{
    /// <summary>
    /// DataBaseInformer
    /// </summary>
    public class DataBaseInformer : IDataInformer
    {
        /// <inheritdoc/>
        public List<string> GetDataNames(DataSet dataSet)
        {
            var names = new List<string>();
            foreach (DataTable table in dataSet.Tables)
            {
                var function = table.TableName.Split(' ')[0].Split(',')[0];
                if (!names.Contains(function))
                    names.Add(function);
            }
            return names;
        }
    }
}
