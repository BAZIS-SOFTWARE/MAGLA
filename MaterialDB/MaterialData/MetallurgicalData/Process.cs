using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialDB.MaterialData.MetallurgicalData
{
    public class Process<T> where T : IConvertible
    {
        public Property Reaction { get; internal set; }
        public string Name { get { return Reaction.Name; } }

        public T TempMax { get; internal set; }

        public T TempMin { get; internal set; }

        public T PhaseMax { get; internal set; }

        public T PhaseMin { get; internal set; }
        public DataTable DataTable { get { return Reaction.DataTable; }  }

        /// <inheritdoc/>

        public override string ToString()
        {
            return $"{Name} : темп {TempMin} - {TempMax} | фаза {PhaseMin} - {PhaseMax}";
        }

        public Process(Property reaction)
        {
            Reaction = reaction;

            var rows = reaction.DataTable.Rows.Cast<DataRow>();

            var tempAr = rows.Select(r => Convert.ToSingle(r[0]));

            TempMin = (T)(IConvertible)Convert.ToSingle(tempAr.First());
            TempMax = (T)(IConvertible)Convert.ToSingle(tempAr.Last());

            IEnumerable<float> phases;

            if(reaction.DataTable.Columns.Count == 2)
            {
                phases = rows.Select(r => Convert.ToSingle(r[1]));
            }
            else
            {
                var colms = reaction.DataTable.Columns.Cast<DataColumn>();
                phases = colms.Skip(1).Select(x => Convert.ToSingle(x.ColumnName.Split('_')[1]));
            }

            PhaseMin = (T)(IConvertible)phases.Min();
            PhaseMax = (T)(IConvertible)phases.Max();

        }
    }
}
