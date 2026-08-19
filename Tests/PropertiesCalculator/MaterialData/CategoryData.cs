using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.MaterialData
{
    [Serializable]
    public class CategoryData : IDictionary<string, Category>
    {
        Dictionary<string, Category> dic = new Dictionary<string, Category>();
        /// <inheritdoc/>
        public Category this[string key] { get { return dic[key]; } set { dic[key] = value; } }
        /// <inheritdoc/>

        public ICollection<string> Keys { get { return dic.Keys; } }
        /// <inheritdoc/>
        public ICollection<Category> Values { get { return dic.Values; } }
        /// <inheritdoc/>
        public int Count { get { return dic.Count; } }
        /// <inheritdoc/>
        public bool IsReadOnly { get { return false; } }
        /// <inheritdoc/>
        public void Add(string key, Category value)
        {
            dic.Add(key, value);
        }
        /// <inheritdoc/>
        public void Add(KeyValuePair<string, Category> item)
        {
            dic.Append(item);
        }
        /// <inheritdoc/>
        public void Clear()
        {
            dic.Clear();
        }
        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, Category> item)
        {
            return dic.Contains(item);
        }
        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            return dic.ContainsKey(key);
        }
        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, Category>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, Category>> GetEnumerator()
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
        public bool Remove(KeyValuePair<string, Category> item)
        {
            return dic.Remove(item.Key);
        }
        /// <inheritdoc/>
        public bool TryGetValue(string key, out Category value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dic.GetEnumerator();
        }
    }
}
