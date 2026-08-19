using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Vector
{
    public class VectorArray<T> where T : INumber<T>
    {
        T[] vector;
        /// <summary>
        /// Length
        /// </summary>
        public int Length { get { return vector.Length; } }

        //public VectorType Type { get; }

        public T this[int row]
        {
            get { return vector[row]; }
            set { vector[row] = value; }
        }

        public VectorArray(T[] input)
        {
            //Type = type;

            vector = new T[input.Length];

            for (int i = 0; i < Length; i++)
            {
                vector[i] = input[i];
            }
        }

        public override string ToString()
        {
            var strAr = new string[vector.Length];
            for (int i = 0; i < vector.Length; i++)
            {
                strAr[i] = vector[i].ToString();             
            }           
            return string.Join(" ", strAr);
        }

        public VectorArray(int length)
        {
            //Type = type;
            vector = new T[length];
        }

        public T[] Vector
        {
            get { return vector; }
        }

        public VectorArray<T> Sum(T[] array)
        {
            var temp = new VectorArray<T>(Length);
            for (int i = 0; i < vector.Length; i++)
            {
                temp[i] = vector[i] + array[i];

            }
            return temp;
        }

        public void Sum(T[] array, VectorArray<T> res)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                res[i] = vector[i] + array[i];

            }
        }

        public VectorArray<T> Sub(T[] array)
        {
            var temp = new VectorArray<T>(Length);
            for (int i = 0; i < vector.Length; i++)
            {
                temp[i] = vector[i] - array[i];

            }
            return temp;
        }

        public void Clear()
        {
            Array.Clear(vector, 0, Length);
        }

        public void Multiply(T v)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] = vector[i] * v;
            }
        }

        public T AbsoluteMaximum()
        {
            return vector.Max(x => T.Abs(x));
        }
    }
}
