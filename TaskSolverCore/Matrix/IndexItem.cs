using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Matrix
{
    public class IndexItem : IComparable<IndexItem>
    {
        public int Value { get; set; }
        public int Start { get; set; }

        public int Stop { get; set; }

        public int CompareTo(IndexItem other)
        {
            if (Value > other.Value) return 1;
            else if (Value < other.Value ) return -1;
            else return 0;
        }

        public override string ToString()
        {
            return Value.ToString() + " : " + Start.ToString() + " " + Stop.ToString();
        }
    }
}
