using MaterialDB.Interfaces;

namespace MaterialDB.MaterialData
{
    /// <summary>
    /// Category
    /// </summary>
    [Serializable]
    public class Category : ICategory
    {
        /// <summary>
        /// Словарь свойств
        /// </summary>
        public Dictionary<PropertyEnum, string> PropertyDic = new Dictionary<PropertyEnum, string>()
        {
            { PropertyEnum.Capacity,"Общие сведения"},
            { PropertyEnum.Density,"Плотность"},
            { PropertyEnum.HardeningFactor,"Коэффициент упрочнения"},
            { PropertyEnum.PoissonRatio,"Коэффициент Пуассона"},
            { PropertyEnum.Structure,"Структура"},
            { PropertyEnum.TensileStrength,"Предел прочности"},
            { PropertyEnum.ThermalConductivity,"Теплопроводность"},
            { PropertyEnum.ThermalExpansion,"ТКЛР"},
            { PropertyEnum.YieldStrength,"Предел текучести"},
            { PropertyEnum.YoungModule,"Модуль Юнга"},
            { PropertyEnum.HardeningModel,"Модель упрочнения"},
            { PropertyEnum.MaterialModel,"Модель материала"}
        };
        /// <inheritdoc/>
        public string Name { get; set; }
/// <inheritdoc/>

        public PropertyData PropertyData { get; set; } = new PropertyData();

        public Property this[string key] 
        { 
            get { return PropertyData[key]; }
            set { PropertyData[key] = value; }
        }
        public Property this[PropertyEnum key]
        {
            get { return PropertyData[PropertyDic[key]]; }
            set { PropertyData[PropertyDic[key]] = value; }
        }
    }
}
