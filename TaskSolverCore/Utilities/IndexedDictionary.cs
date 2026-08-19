using Model.ObjectsCollections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Utilities
{
    public class IndexedDictionary<TKey, TValue> : Dictionary<TKey, TValue>  where TKey : notnull
    {
        //private readonly Dictionary<TKey, TValue> _dict = new();
        private readonly List<TKey> _keys = new();

        public void AddItem(TKey key, TValue value)
        {       
            Add(key, value);
            _keys.Add(key);
        }

        // O(1) получение ключа по числовому индексу
        public TKey GetKeyByIndex(int index) => _keys[index];
    }
}
