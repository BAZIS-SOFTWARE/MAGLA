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
                    throw new Exception($"Материал {Name} не содержит категорию \"Механические свойства\"!");

                if (!this["Механические свойства"].PropertyData.ContainsKey("Предел текучести"))
                    throw new Exception($"Материал {Name} не содержит свойство \"Предел текучести\"!");
                if (!this["Механические свойства"].PropertyData.ContainsKey("Предел прочности"))
                    throw new Exception($"Материал {Name} не содержит свойство \"Предел прочности\"!");
                if (!this["Механические свойства"].PropertyData.ContainsKey("Коэффициент упрочнения"))
                    throw new Exception($"Материал {Name} не содержит свойство \"Коэффициент упрочнения\"!");
                if (!this["Механические свойства"].PropertyData.ContainsKey("Модуль Юнга"))
                    throw new Exception($"Материал {Name} не содержит свойство \"Модуль Юнга\"!");
                if (!this["Механические свойства"].PropertyData.ContainsKey("ТКЛР"))
                    throw new Exception($"Материал {Name} не содержит свойство \"ТКЛР\"!");

            return true;
        }


        /// <summary>
        /// CheckThermalProps
        /// </summary>
        /// <exception cref="Exception"></exception>
        public bool CheckThermalProps()
        {
 
                if (!CategoryData.ContainsKey("Тепловые свойства"))
                    throw new Exception($"Материал {Name} не содержит категорию \"Тепловые свойства\"!");

                if (!this["Тепловые свойства"].PropertyData.ContainsKey("Теплопроводность"))
                    throw new Exception($"Материал {Name} не содержит свойство \"Теплопроводность\"!");
                if (!this["Тепловые свойства"].PropertyData.ContainsKey("Теплоемкость"))
                    throw new Exception($"Материал {Name} не содержит свойство \"Теплоемкость\"!");
                if (!this["Тепловые свойства"].PropertyData.ContainsKey("Плотность"))
                    throw new Exception($"Материал {Name} не содержит свойство \"Плотность\"!");

            return true;
            
        }

        /// <summary>
        /// CheckPhaseData
        /// </summary>
        /// <exception cref="Exception"></exception>
        public bool CheckPhaseData()
        {
            if (!CategoryData.ContainsKey("Общие сведения"))
                throw new Exception($"Материал {Name} не содержит категорию \"Общие сведения\"!");

            if (!this["Общие сведения"].PropertyData.ContainsKey("Структура"))
                throw new Exception($"Материал {Name} не содержит свойство \"Структура\"!");

            var phaseTable = this["Общие сведения"]["Структура"].DataTable;

            if (phaseTable == null)
                throw new Exception("Ошибка загрузки данных структуры материала! \nПроверьте раздел \"Структура\"");

            if (phaseTable.Rows.Count == 0)
                throw new Exception("Ошибка загрузки данных структуры материала! \nПроверьте колличество фаз!");
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
