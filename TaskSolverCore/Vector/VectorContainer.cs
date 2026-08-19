using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Vector
{
    public enum VectorType { force,reaction,result,plastic}
    public class VectorContainer<T> where T : INumber<T>
    {
        Dictionary<VectorType,VectorArray<T>> vectorArrays;
        //VectorList<T> vectorList;

        public VectorContainer()
        {
            vectorArrays = new Dictionary<VectorType,VectorArray<T>>();
        }

        public void AddVector(VectorType type, int length)
        {
            vectorArrays.Add(type,new VectorArray<T>(length));
        }

        public void AddVector(VectorType type, VectorArray<T> vector)
        {
            vectorArrays.Add(type, vector);
        }

        public VectorArray<T> GetVectorArray(VectorType type)
        {
            return vectorArrays[type];
        }

        public void RemoveVectors(VectorType type)
        {
            vectorArrays.Remove(type);
        }
    }
}
