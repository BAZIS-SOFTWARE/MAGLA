using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MaterialDB.MaterialData.MetallurgicalData
{
    public class ProcessData : IEnumerable<Process<float>>
    {
        List<Process<float>> processes { get; } = new List<Process<float>>();        

/// <inheritdoc/>

        public override string ToString()
        {
            return string.Join(";", processes);
        }

        public IEnumerator<Process<float>> GetEnumerator()
        {
            foreach (var reaction in processes)
            {
                yield return reaction;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// CreateProcessData
        /// </summary>
        /// <param name="reactions"></param>
        /// <param name="reacTypes"></param>
        public ProcessData(IEnumerable<Property> reactions, string[] reacTypes)
        {
            foreach (var reacType in reacTypes)
            {
                var reacs = reactions.Where(r => r.Name.Split(' ')[0] == reacType);

                foreach (var reac in reacs)
                {
                    var process = new Process<float>(reac);
                    processes.Add(process);
                }

            }
        }
    }
}
