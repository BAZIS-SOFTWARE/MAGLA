using System;

namespace MaterialDB.MaterialData.MetallurgicalData
{
    /// <summary>
    /// Phase
    /// </summary>
    [Serializable]
    public class Phase
    {
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Value
        /// </summary>
        public float Value { get; set; }

/// <inheritdoc/>

        public override string ToString()
        {
            return Name + " " + Value.ToString();
        }
    }
}
