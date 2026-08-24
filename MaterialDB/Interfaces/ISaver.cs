using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialDB.Interfaces
{
    /// <summary>
    /// Interface for data saving
    /// </summary>
    public interface ISaver
    {
        /// <summary>
        /// SaveDataBase
        /// </summary>
        /// <param name="data"></param>
        /// <param name="path"></param>
        void SaveDataBase(DataSet data, string path);
    }
}
