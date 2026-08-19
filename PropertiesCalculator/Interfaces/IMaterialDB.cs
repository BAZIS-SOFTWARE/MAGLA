using PropertiesCalculator.MaterialData;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertiesCalculator.Interfaces
{
    /// <summary>
    /// IMaterialDB
    /// </summary>
    public interface IMaterialDB : IDictionary<string, MaterialDBItem>
    {
        /// <summary>
        /// Name
        /// </summary>
        string Name { get; set; }
        /// <summary>
        /// GetPhases
        /// </summary>
        /// <param name="materialsNames"></param>
        /// <returns></returns>
        HashSet<string> GetPhases(List<string> materialsNames);
    }
}
