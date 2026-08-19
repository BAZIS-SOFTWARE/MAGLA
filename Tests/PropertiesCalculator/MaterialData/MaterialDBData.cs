using PropertiesCalculator.Interfaces;
using MathNet.Numerics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.MaterialData
{
    /// <summary>
    /// Категории
    /// </summary>
    public enum CategoryEnum
    {
        /// <summary>
        /// Общие сведения
        /// </summary>
        General,
        /// <summary>
        /// Тепловые свойства
        /// </summary>
        Thermal,
        /// <summary>
        /// Механические свойства
        /// </summary>
        Mechanical,
        /// <summary>
        /// Металлургия
        /// </summary>
        Metallurgical
    }

    /// <summary>
    /// Категории
    /// </summary>
    public enum PropertyEnum
    {
        /// <summary>
        /// Структура
        /// </summary>
        Structure,
        /// <summary>
        /// Плотность
        /// </summary>
        Density,
        /// <summary>
        /// Теплопроводность
        /// </summary>
        ThermalConductivity,
        /// <summary>
        /// Теплоемкость
        /// </summary>
        Capacity,
        /// <summary>
        /// Модуль Юнга
        /// </summary>
        YoungModule,
        /// <summary>
        /// Коэффициент Пуассона
        /// </summary>
        PoissonRatio,
        /// <summary>
        /// Коэффициент упрочнения
        /// </summary>
        HardeningFactor,
        /// <summary>
        /// Предел текучести
        /// </summary>
        YieldStrength,
        /// <summary>
        /// Предел прочности
        /// </summary>
        TensileStrength,
        /// <summary>
        /// ТКЛР
        /// </summary>
        ThermalExpansion

    }

    [Serializable]
    public class MaterialDBData : IMaterialDB
    {
        /// <inheritdoc/>
        public string Name { get; set; }
        /// <summary>
        /// Словарь категорий
        /// </summary>
        public static Dictionary<CategoryEnum, string> CategoryDic = new Dictionary<CategoryEnum, string>()
        {
            { CategoryEnum.General,"Общие сведения"},
            { CategoryEnum.Thermal,"Тепловые свойства"},
            { CategoryEnum.Mechanical,"Механические свойства"},
            { CategoryEnum.Metallurgical,"Металлургия"}
        };
        /// <summary>
        /// Словарь свойств
        /// </summary>
        public static Dictionary<PropertyEnum, string> PropertyDic = new Dictionary<PropertyEnum, string>()
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
        };
        Dictionary<string, MaterialDBItem> materials { get; } = new Dictionary<string, MaterialDBItem>();
        /// <inheritdoc/>
        public MaterialDBItem this[string key] { get { return materials[key]; } set { materials[key] = value; } }
        /// <inheritdoc/>

        public ICollection<string> Keys { get { return materials.Keys; } }
        /// <inheritdoc/>
        public ICollection<MaterialDBItem> Values { get { return materials.Values; } }
        /// <inheritdoc/>
        public int Count { get { return materials.Count; } }
        /// <inheritdoc/>
        public bool IsReadOnly { get { return false; } }
        /// <inheritdoc/>
        public void Add(string key, MaterialDBItem value)
        {
            materials.Add(key, value);
        }
        /// <inheritdoc/>
        public void Add(KeyValuePair<string, MaterialDBItem> item)
        {
            materials.Append(item);
        }
        /// <inheritdoc/>
        public void Clear()
        {
            materials.Clear();
        }
        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, MaterialDBItem> item)
        {
            return materials.Contains(item);
        }
        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            return materials.ContainsKey(key);
        }
        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, MaterialDBItem>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, MaterialDBItem>> GetEnumerator()
        {
            foreach (var item in materials)
            {
                yield return item;
            }
        }
        /// <inheritdoc/>
        public bool Remove(string key)
        {
            return materials.Remove(key);
        }
        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, MaterialDBItem> item)
        {
            return materials.Remove(item.Key);
        }
        /// <inheritdoc/>
        public bool TryGetValue(string key, out MaterialDBItem value)
        {
            return materials.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return materials.GetEnumerator();
        }

        
/// <inheritdoc/>

        public HashSet<string> GetPhases(List<string> materialsNames)
        {
            var phaseList = new HashSet<string>();

            foreach (var matElems in materialsNames)
            {
                foreach (var phase in materials[matElems].GetPhases())
                {
                    if (!phaseList.Contains(phase))
                        phaseList.Add(phase);
                }
            }
            return phaseList;
        }

    }
}
