using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Utilities
{
    public class VertexEqualityComparer : IEqualityComparer<Vertex>, IComparer<Vertex>
    {
        public int Compare(Vertex x, Vertex y)
        {
            if (x.Index < y.Index)
                return -1;
            else if (x.Index > y.Index)
                return 1;
            else return 0;
        }

        public bool Equals(Vertex v1, Vertex v2)
        {
            if (v1 == null && v2 == null)
                return true;
            else if (v1 == null || v2 == null)
                return false;
            else if (v1.Index == v2.Index)
                return true;
            else
                return false;
        }

        public int GetHashCode(Vertex v)
        {
            int hCode = v.Index;
            return hCode.GetHashCode();
        }
    }
}
