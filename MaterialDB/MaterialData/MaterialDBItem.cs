using MaterialDB.MaterialData.MetallurgicalData;
using System;
using System.Collections.Generic;
using System.Data;

namespace MaterialDB.MaterialData
{
    /// <summary>
    /// MaterialItem
    /// </summary>
    [Serializable]
    public class MaterialDBItem
    {
        public MaterialDBItem(string name)
        {
            Name = name;
        }

        internal MaterialDBItem()
        {
        }

        /// <summary>
        /// Словарь категорий
        /// </summary>
        public Dictionary<CategoryEnum, string> CategoryDic = new Dictionary<CategoryEnum, string>()
        {
            { CategoryEnum.General,"Общие сведения"},
            { CategoryEnum.Thermal,"Тепловые свойства"},
            { CategoryEnum.Mechanical,"Механические свойства"},
            { CategoryEnum.Metallurgical,"Металлургия"}
        };
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

        public Category this[CategoryEnum key]
        {
            get { return CategoryData[CategoryDic[key]]; }
            set { CategoryData[CategoryDic[key]] = value; }
        }
        /// <summary>
        /// Copy
        /// </summary>
        /// <param name="copyName"></param>
        /// <returns></returns>
        public MaterialDBItem Copy(string copyName)
        {
            var newMaterial = new MaterialDBItem() { Name = copyName };

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

        public void AddGeneralProps()
        {
            if (!CategoryData.ContainsKey("Общие сведения"))
                CategoryData.Add("Общие сведения", new Category());
        }

        public void AddMetallurgicalProps()
        {
            if (!CategoryData.ContainsKey("Металлургия"))
                CategoryData.Add("Металлургия", new Category());
        }

        public void AddPhaseData(PhaseData phases)
        {
            if (!CategoryData.ContainsKey("Общие сведения"))
                throw new GeneralAbsentException();

            var table = new DataTable();
            table.Columns.Add("Фаза", typeof(string));
            table.Columns.Add("Масс.доли", typeof(double));

            foreach (var item in phases)
            {
                var row = table.NewRow();
                row[0] = item.Name;
                row[1] = item.Value;
                table.Rows.Add(row);
            }

            this[CategoryEnum.General].PropertyData.Add("Структура",
new Property("Структура", "", "", "", table));
        }

        public void AddCrystallization(double ts, double tl)
        {
            if (!CategoryData.ContainsKey("Общие сведения"))
                throw new GeneralAbsentException();

            var table = new DataTable();
            table.Columns.Add("Точка", typeof(string));
            table.Columns.Add("Температура", typeof(double));

            var row = table.NewRow();
            row[0] = "TS";
            row[1] = ts;
            table.Rows.Add(row);

            row = table.NewRow();
            row[0] = "TL";
            row[1] = tl;
            table.Rows.Add(row);

            this[CategoryEnum.General].PropertyData.Add("Кристаллизация",
new Property("Кристаллизация", "", "", "", table));
        }

        public void AddMaterialModel(int mode)
        {
            if (!CategoryData.ContainsKey("Общие сведения"))
                throw new GeneralAbsentException();

            var table = new DataTable();
            table.Columns.Add("Модель материала", typeof(int));

            var row = table.NewRow();
            row[0] = mode;
            table.Rows.Add(row);

            this[CategoryEnum.General].PropertyData.Add("Модель материала",
new Property("Модель материала", "", "", "", table));
        }

        public void AddHardeningModel(int mode)
        {
            if (!CategoryData.ContainsKey("Общие сведения"))
                throw new GeneralAbsentException();

            var table = new DataTable();
            table.Columns.Add("Модель упрочнения", typeof(int));

            var row = table.NewRow();
            row[0] = mode;
            table.Rows.Add(row);

            this[CategoryEnum.General].PropertyData.Add("Модель упрочнения",
new Property("Модель упрочнения", "", "", "", table));
        }

        public void AddThermalProps()
        {
            if (!CategoryData.ContainsKey("Тепловые свойства"))
                CategoryData.Add("Тепловые свойства", new Category());

            if (!this["Тепловые свойства"].PropertyData.ContainsKey("Теплопроводность"))
            {
                var table = CreateTable();
                this[CategoryEnum.Thermal].PropertyData.Add("Теплопроводность",
    new Property("Теплопроводность", "", "", "", table));
            }

            if (!this["Тепловые свойства"].PropertyData.ContainsKey("Теплоемкость"))
            {
                var table = CreateTable();
                this[CategoryEnum.Thermal].PropertyData.Add("Теплоемкость",
    new Property("Теплоемкость", "", "", "", table));
            }

            if (!this["Тепловые свойства"].PropertyData.ContainsKey("Плотность"))
            {
                var table = CreateTable();
                this[CategoryEnum.Thermal].PropertyData.Add("Плотность",
    new Property("Плотность", "", "", "", table));
            }
        }

        public void AddMechProps()
        {
            if (!CategoryData.ContainsKey("Механические свойства"))
                CategoryData.Add("Механические свойства", new Category());

            if (!this[CategoryEnum.Mechanical].PropertyData.ContainsKey("Предел текучести"))
            {
                var table = CreateTable();
                this[CategoryEnum.Mechanical].PropertyData.Add("Предел текучести",
    new Property("Предел текучести", "", "", "", table));
            }

            if (!this[CategoryEnum.Mechanical].PropertyData.ContainsKey("Предел прочности"))
            {
                var table = CreateTable();
                this[CategoryEnum.Mechanical].PropertyData.Add("Предел прочности",
    new Property("Предел прочности", "", "", "", table));
            }

            if (!this[CategoryEnum.Mechanical].PropertyData.ContainsKey("Коэффициент упрочнения"))
            {
                var table = CreateTable();
                this[CategoryEnum.Mechanical].PropertyData.Add("Коэффициент упрочнения",
    new Property("Коэффициент упрочнения", "", "", "", table));
            }

            if (!this[CategoryEnum.Mechanical].PropertyData.ContainsKey("Модуль Юнга"))
            {
                var table = CreateTable();
                this[CategoryEnum.Mechanical].PropertyData.Add("Модуль Юнга",
    new Property("Модуль Юнга", "", "", "", table));
            }

            if (!this[CategoryEnum.Mechanical].PropertyData.ContainsKey("ТКЛР"))
            {
                var table = CreateTable();
                this[CategoryEnum.Mechanical].PropertyData.Add("ТКЛР",
    new Property("ТКЛР", "", "", "", table));
            }
        }

        private DataTable CreateTable()
        {
            var table = new DataTable();
            table.Columns.Add("Температура", typeof(double));

            foreach (var item in GetPhases())
                table.Columns.Add(item, typeof(double));

            return table;
        }
    }
}
