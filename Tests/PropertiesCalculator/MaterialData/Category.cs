using PropertiesCalculator.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.MaterialData
{
    /// <summary>
    /// Category
    /// </summary>
    [Serializable]
    public class Category : ICategory
    {
        /// <inheritdoc/>
        public string Name { get; set; }
/// <inheritdoc/>

        public PropertyData PropertyData { get; set; } = new PropertyData();

        public Property this[string key] 
        { 
            get { return PropertyData[key]; }
            set { PropertyData[key] = value; }
        }
    }
}
