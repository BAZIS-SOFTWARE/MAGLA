using System;
using System.Collections.Generic;
using System.Data;

namespace PropertiesCalculator.MaterialData
{
    /// <summary>
    /// MaterialItem
    /// </summary>
    [Serializable]
    public class MaterialDBItem
    {
        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
        public string Name { get; set; }

        public CategoryData CategoryData { get; set; } = new CategoryData();

        public Category this[string key]
        {
            get { return CategoryData[key]; }
            set { CategoryData[key] = value; }
        }
        /// <summary>
        /// Copy
        /// </summary>
        /// <param name="copyName"></param>
        /// <returns></returns>
        public MaterialDBItem Copy(string copyName)
        {
            var newMaterial = new MaterialDBItem { Name = copyName };

            foreach (var category in CategoryData.Values)
            {
                newMaterial.CategoryData.Add(category.Name, new Category() { Name = category.Name });
                foreach (var property in category.PropertyData)
                {
                    var prop = property.Value.Copy(property.Key);
                    newMaterial[category.Name].PropertyData.Add(property.Key, prop);
                }
            }
            return newMaterial;
        }
        /// <summary>
        /// CheckMechanicalProps
        /// </summary>
        /// <exception cref="Exception"></exception>
        public bool CheckMechanicalProps()
        {

                if (!CategoryData.ContainsKey("Механические свойства"))
                    throw new Exception($"Material {Name} does not contain the \"Mechanical properties\" category.");

                if (!this["Механические свойства"].PropertyData.ContainsKey("Предел текучести"))
                    throw new Exception($"Material {Name} does not contain the \"Yield strength\" property.");
                if (!this["Механические свойства"].PropertyData.ContainsKey("Предел прочности"))
                    throw new Exception($"Material {Name} does not contain the \"Ultimate strength\" property.");
                if (!this["Механические свойства"].PropertyData.ContainsKey("Коэффициент упрочнения"))
                    throw new Exception($"Material {Name} does not contain the \"Hardening coefficient\" property.");
                if (!this["Механические свойства"].PropertyData.ContainsKey("Модуль Юнга"))
                    throw new Exception($"Material {Name} does not contain the \"Young's modulus\" property.");
                if (!this["Механические свойства"].PropertyData.ContainsKey("ТКЛР"))
                    throw new Exception($"Material {Name} does not contain the \"CTE\" property.");

            return true;
        }


        /// <summary>
        /// CheckThermalProps
        /// </summary>
        /// <exception cref="Exception"></exception>
        public bool CheckThermalProps()
        {
 
                if (!CategoryData.ContainsKey("Тепловые свойства"))
                    throw new Exception($"Material {Name} does not contain the \"Thermal properties\" category.");

                if (!this["Тепловые свойства"].PropertyData.ContainsKey("Теплопроводность"))
                    throw new Exception($"Material {Name} does not contain the \"Thermal conductivity\" property.");
                if (!this["Тепловые свойства"].PropertyData.ContainsKey("Теплоемкость"))
                    throw new Exception($"Material {Name} does not contain the \"Heat capacity\" property.");
                if (!this["Тепловые свойства"].PropertyData.ContainsKey("Плотность"))
                    throw new Exception($"Material {Name} does not contain the \"Density\" property.");

            return true;
            
        }

        /// <summary>
        /// CheckPhaseData
        /// </summary>
        /// <exception cref="Exception"></exception>
        public bool CheckPhaseData()
        {
            if (!CategoryData.ContainsKey("Общие сведения"))
                throw new Exception($"Material {Name} does not contain the \"General information\" category.");

            if (!this["Общие сведения"].PropertyData.ContainsKey("Структура"))
                throw new Exception($"Material {Name} does not contain the \"Structure\" property.");

            var phaseTable = this["Общие сведения"]["Структура"].DataTable;

            if (phaseTable == null)
                throw new Exception("Failed to load material structure data.\nCheck the \"Structure\" section.");

            if (phaseTable.Rows.Count == 0)
                throw new Exception("Failed to load material structure data.\nCheck the number of phases.");
            return true;
        }
        /// <summary>
        /// GetPhases
        /// </summary>
        /// <returns></returns>

        public IEnumerable<string> GetPhases()
        {
            var phases = this["Общие сведения"]["Структура"].DataTable;

            foreach (DataRow phaseRow in phases.Rows)
            {
                yield return phaseRow[0].ToString();
            }
        }

    }
}
