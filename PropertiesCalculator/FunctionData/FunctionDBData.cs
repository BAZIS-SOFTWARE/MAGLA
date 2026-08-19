using PropertiesCalculator.MaterialData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PropertiesCalculator.FunctionData
{
    /// <summary>
    /// FunctionItem
    /// </summary>
    [Serializable]
    public class FunctionDBData : IDictionary<string, Property>
    {
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        Dictionary<string, Property> dic = new Dictionary<string, Property>();
        /// <inheritdoc/>
        public Property this[string key] { get { return dic[key]; } set { dic[key] = value; } }
        /// <inheritdoc/>

        public ICollection<string> Keys { get { return dic.Keys; } }
        /// <inheritdoc/>
        public ICollection<Property> Values { get { return dic.Values; } }
        /// <inheritdoc/>
        public int Count { get { return dic.Count; } }
        /// <inheritdoc/>
        public bool IsReadOnly { get { return false; } }
        /// <inheritdoc/>
        public void Add(string key, Property value)
        {
            dic.Add(key, value);
        }
        /// <inheritdoc/>
        public void Add(KeyValuePair<string, Property> item)
        {
            dic.Add(item.Key, item.Value);
        }
        /// <inheritdoc/>
        public void Clear()
        {
            dic.Clear();
        }
        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, Property> item)
        {
            return dic.Contains(item);
        }
        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            return dic.ContainsKey(key);
        }
        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, Property>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, Property>> GetEnumerator()
        {
            foreach (var item in dic)
            {
                yield return item;
            }
        }
        /// <inheritdoc/>
        public bool Remove(string key)
        {
            return dic.Remove(key);
        }
        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, Property> item)
        {
            return dic.Remove(item.Key);
        }
        /// <inheritdoc/>
        public bool TryGetValue(string key, out Property value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dic.GetEnumerator();
        }
    }
}
