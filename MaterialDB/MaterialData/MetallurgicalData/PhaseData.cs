using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace MaterialDB.MaterialData.MetallurgicalData
{
    /// <summary>
    /// PhaseData
    /// </summary>
    [Serializable]
    public class PhaseData : IList<Phase>
    {
        List<Phase> phases = new List<Phase>();

        public Phase this[int index] { get { return phases[index]; } set { phases[index] = value; } }
/// <inheritdoc/>

        public int Count { get { return phases.Count; } }
/// <inheritdoc/>

        public bool IsReadOnly
        {
            get { return false; }
        }
        /// <summary>
        /// PhaseData
        /// </summary>
        /// <param name="phaseTable"></param>
        public PhaseData(DataTable phaseTable)
        {
            for (int i = 0; i < phaseTable.Rows.Count; i++)
            {
                var phase = new Phase();
                phase.Name = phaseTable.Rows[i][0].ToString();
                phase.Value = Convert.ToSingle(phaseTable.Rows[i][1]);

                phases.Add(phase);
            }
        }
        /// <summary>
        /// PhaseData
        /// </summary>
        public PhaseData()
        {

        }

        public PhaseData(List<Phase> phases)
        {
            this.phases = phases;
        }

        public override string ToString()
        {
            return string.Join(";", phases);
        }

        public Phase Find(string phaseName)
        {
            foreach (Phase phase in phases)
            {
                if (phase.Name == phaseName)
                {
                    return phase;
                }
            }

            return null;
        }

        public void Add(Phase item)
        {
            phases.Add(item);
        }
        public void AddRange(IEnumerable<Phase> items)
        {
            phases.AddRange(items);
        }

        public void Clear()
        {
            phases.Clear();
        }

        public bool Contains(Phase item)
        {
            return phases.Contains(item);
        }

        public void CopyTo(Phase[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<Phase> GetEnumerator()
        {
            foreach (var phase in phases)
            {
                yield return phase;
            }
        }

        public int IndexOf(Phase item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, Phase item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(Phase item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return phases.GetEnumerator();
        }
    }
}
