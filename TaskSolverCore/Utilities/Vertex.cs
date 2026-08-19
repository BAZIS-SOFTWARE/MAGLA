using System;
using System.Collections.Generic;
using System.Linq;

namespace TaskSolverCore.Utilities
{
    /// <summary>
    /// Mark
    /// </summary>
    public enum Mark
    {
        /// <summary>
        /// Visited
        /// </summary>
        Visited,
        /// <summary>
        /// NotVisited
        /// </summary>
        NotVisited
    }
    /// <summary>
    /// Vertex
    /// </summary>
    public class Vertex : IEqualityComparer<Vertex>, IComparer<Vertex>
    {

        private HashSet<Vertex> incidentVerts = new HashSet<Vertex>();
        //int number;
        int index;
        //public int Number { get { return number; } }
        public int Index { get { return index; } }
        public Mark Mark{ get; set; }

        public Vertex(int index)
        {
            //this.number = number;
            this.index = index;
            Mark = Mark.NotVisited;
        }

        public IEnumerable<Vertex> IncidentVerts
        {
            get
            {
                foreach (var item in incidentVerts)
                {
                    yield return item;
                }
            }

            
        }
        /// <summary>
        /// ClearNorExisted
        /// </summary>
        public void ClearNorExisted()
        {
            incidentVerts = incidentVerts.Where(x => x != null).ToHashSet();
        }
/// <inheritdoc/>

        public int Compare(Vertex x, Vertex y)
        {
            if (x.Index < y.Index)
                return -1;
            else if (x.Index > y.Index)
                return 1;
            else return 0;
        }
/// <inheritdoc/>

        public bool Equals(Vertex v1, Vertex v2)
        {
            if (v1.Index == v2.Index)
                return true;
            else
                return false;
        }
/// <inheritdoc/>

        public int GetHashCode(Vertex v)
        {
            return Index;
        }
/// <inheritdoc/>

        public override string ToString()
        {
            var incidentNodesNumbs = incidentVerts.Select(x => x.Index.ToString());

            return String.Format("{0} : {1}", Index, String.Join(",", incidentNodesNumbs));
        }
        public bool Connect(Vertex node)
        {
            //if (!graph.Verts.Contains(node1) || !graph.Verts.Contains(node2)) throw new ArgumentException();
            if(!incidentVerts.Contains(node) & Index != node.Index)
            {
                incidentVerts.Add(node);
                node.incidentVerts.Add(this);
                return true;
            }
            return false;
        }

        public bool Disconnect(Vertex node)
        {
            //if (!graph.Verts.Contains(node1) || !graph.Verts.Contains(node2)) throw new ArgumentException();
            if (incidentVerts.Contains(node) & Index != node.Index)
            {
                incidentVerts.Remove(node);
                node.incidentVerts.Remove(this);
                return true;
            }
            return false;
        }

        internal void ChangeIndex(int change)
        {
            index = change;
        }
    }
}
