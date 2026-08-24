using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialDB.Interfaces
{
    /// <summary>
    /// Interface for data loading
    /// </summary>
    public interface ILoader
    {
        /// <summary>
        /// LoadDataBase
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        DataSet LoadDataBase(string path);
    }
}
