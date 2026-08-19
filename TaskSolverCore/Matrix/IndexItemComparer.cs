using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Matrix
{
    public class IndexItemComparer : IComparer<IndexItem>
    {
        public int Compare(IndexItem x, IndexItem y)
        {
            if (x.Value > y.Value)
                return 1;
            else if (x.Value < y.Value)
                return -1;
            else return 0;
        }
    }
}
