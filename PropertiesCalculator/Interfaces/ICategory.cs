using PropertiesCalculator.MaterialData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.Interfaces
{
    /// <summary>
    /// ICategory
    /// </summary>
    public interface ICategory
    {
        /// <summary>
        /// Name
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// PropertyData
        /// </summary>
        PropertyData PropertyData { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Property this[string key] { get; set; }
    }
}
