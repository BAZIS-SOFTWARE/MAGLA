using System;

namespace PropertiesCalculator.MaterialData.MetallurgicalData
{
    /// <summary>
    /// Phase
    /// </summary>
    [Serializable]
    public class Phase<T> where T : IConvertible
    {
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Value
        /// </summary>
        public T Value { get; set; }

/// <inheritdoc/>

        public override string ToString()
        {
            return Name + " " + Value.ToString();
        }
    }
}
