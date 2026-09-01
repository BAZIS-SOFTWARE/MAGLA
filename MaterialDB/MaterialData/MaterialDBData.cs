using MaterialDB.Interfaces;
using Newtonsoft.Json;
using System.Collections;
//using System.Xml;

namespace MaterialDB.MaterialData
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
        ThermalExpansion,
        /// <summary>
        /// Модель упрочнения
        /// </summary>
        HardeningModel,
        /// <summary>
        /// Модель материала
        /// </summary>
        MaterialModel
    }

    [Serializable]
    public class MaterialDBData : IMaterialDB
    {
        /// <inheritdoc/>
        public string Name { get; set; }

        Dictionary<string, MaterialDBItem> materials { get; } = new Dictionary<string, MaterialDBItem>();
        /// <inheritdoc/>
        public MaterialDBItem this[string key] 
        { 
            get { return materials[key]; } 
            set { materials[key] = value; } 
        }
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
        // для поддержки сериализации 
        public MaterialDBData() 
        {
            Name = "newMaterialsDb";
        }

        public MaterialDBData(string dbName, string dbFolder) : this()
        {

            var filePath = FindFileByPath(dbFolder, dbName);
            if (filePath == null)
            {
                throw new Exception($"Database {dbName} was not found in folder {dbFolder}.");
            }

            else
            {
                var settingsSerializer = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Formatting = Formatting.Indented,
                };

                var db = JsonConvert.DeserializeObject<MaterialDBData>
   (File.ReadAllText($@"{dbFolder}\{dbName}"), settingsSerializer);

                Name = dbName;
                materials = db.materials;
            }

        }

        private string? FindFileByPath(string folder, string fileName)
        {
            var projFiles = Directory.GetFiles(folder, fileName, SearchOption.TopDirectoryOnly);
            if (projFiles.Count() > 0)
            {
                return Path.GetDirectoryName(projFiles[0]);
            }

            return null;
        }

    }
}
