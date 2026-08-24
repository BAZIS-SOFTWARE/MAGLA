using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialDB.Interfaces
{
    /// <summary>
    /// Inteface for info
    /// </summary>
    public interface IDataInformer
    {
        /// <summary>
        /// GetDataNames
        /// </summary>
        /// <param name="dataSet"></param>
        /// <returns></returns>
        List<string> GetDataNames(DataSet dataSet);
    }
}
