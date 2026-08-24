using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialDB.MaterialData
{
    public enum ChemElement : int { C, Mn, Mo, Si, Cr, Ni, V, W, Al, Cu, P, As, Ti, Nb, None };
    /// <summary>
    /// ChemicalData
    /// </summary>
    [Serializable]
    public class ChemicalData : IDictionary<ChemElement, float>
    {
        Dictionary<ChemElement, float> chemicalElements = new Dictionary<ChemElement, float>
            {
                { ChemElement.C, 0.0f },
                { ChemElement.Mn, 0.0f },
                { ChemElement.Mo, 0.0f },
                { ChemElement.Si, 0.0f },
                { ChemElement.Cr, 0.0f },
                { ChemElement.Ni, 0.0f },
                { ChemElement.V, 0.0f },
                { ChemElement.W, 0.0f },
                { ChemElement.Al, 0.0f },
                { ChemElement.Cu, 0.0f },
                { ChemElement.P, 0.0f },
                { ChemElement.As, 0.0f },
                { ChemElement.Ti, 0.0f },
                { ChemElement.Nb, 0.0f }
            };
        public float this[ChemElement key] 
        { 
            get { return chemicalElements[key]; } 
            set { chemicalElements[key] = value; }
        }
        /// <summary>
        /// Parce string to chem element
        /// </summary>
        /// <param name="el"></param>
        /// <returns></returns>
        public ChemElement Parce(string el)
        {
            switch (el)
            {
                case "C":
                    return ChemElement.C;
                case "Si":
                    return ChemElement.Si;
                case "Mn":
                    return ChemElement.Mn;
                case "Cr":
                    return ChemElement.Cr;
                case "Cu":
                    return ChemElement.Cu;
                case "Mo":
                    return ChemElement.Mo;
                case "Ni":
                    return ChemElement.Ni;
                case "V":
                    return ChemElement.V;
                case "Al":
                    return ChemElement.Al;
                case "P":
                    return ChemElement.P;
                case "Ti":
                    return ChemElement.Ti;
                case "As":
                    return ChemElement.As;
                case "W":
                    return ChemElement.W;
                case "Nb":
                    return ChemElement.Nb;
                default:
                    return ChemElement.None;

            }
        }

        public ICollection<ChemElement> Keys { get { return chemicalElements.Keys; } }

        public ICollection<float> Values { get { return chemicalElements.Values; } }

        public int Count { get { return chemicalElements.Count; } }

        public bool IsReadOnly { get { return false; } }

        public void Add(ChemElement key, float value)
        {
            chemicalElements.Add(key, value);
        }

        public void Add(KeyValuePair<ChemElement, float> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            chemicalElements.Clear();
        }

        public bool Contains(KeyValuePair<ChemElement, float> item)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(ChemElement key)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<ChemElement, float>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<ChemElement, float>> GetEnumerator()
        {
            foreach (var chemicalElement in chemicalElements)
            {
                yield return chemicalElement;
            }
        }

        public bool Remove(ChemElement key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<ChemElement, float> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(ChemElement key, out float value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
