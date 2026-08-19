using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.MaterialData
{
    /// <summary>
    /// ValueItem
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Parameter<T>
    {
        /// <summary>
        /// Temperature
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Value
        /// </summary>
        public T Value { get; set; }
/// <inheritdoc/>

        public override string ToString()
        {
            return $"{Name} {Value}";
        }
    }
}
