using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Matrix.Utilities
{
    /// <summary>
    /// MergeSortWithComparer
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class MergeSortWithComparer<T> where T : IComparable<T>
    {
        static T[] temporaryArray;

        static void Merge(List<T> array, int start, int middle, int end, IComparer<T> comparer)
        {
            var leftPtr = start;
            var rightPtr = middle + 1;
            var length = end - start + 1;
            for (int i = 0; i < length; i++)
            {
                if (rightPtr > end || (leftPtr <= middle && comparer.Compare(array[leftPtr], array[rightPtr]) < 0))
                {
                    temporaryArray[i] = array[leftPtr];
                    leftPtr++;
                }
                else
                {
                    temporaryArray[i] = array[rightPtr];
                    rightPtr++;
                }
            }
            for (int i = 0; i < length; i++)
                array[i + start] = temporaryArray[i];
        }

        static void MergeSortFunction(List<T> array, int start, int end, IComparer<T> comparer)
        {
            if (start == end) return;
            var middle = (start + end) / 2;
            MergeSortFunction(array, start, middle, comparer);
            MergeSortFunction(array, middle + 1, end, comparer);
            Merge(array, start, middle, end, comparer);

        }

        public static void MergeSortFunction(List<T> array, IComparer<T> comparer)
        {
            if (array.Count == 0) throw new Exception("пустой массив!");
            temporaryArray = new T[array.Count];
            MergeSortFunction(array, 0, array.Count - 1, comparer);
        }
    }

    public static class MergeSort<T> where T : IComparable<T>
    {
        static T[] temporaryArray;

        static void Merge(List<T> array, int start, int middle, int end)
        {
            var leftPtr = start;
            var rightPtr = middle + 1;
            var length = end - start + 1;
            for (int i = 0; i < length; i++)
            {
                if (rightPtr > end || (leftPtr <= middle && array[leftPtr].CompareTo(array[rightPtr]) < 0))
                {
                    temporaryArray[i] = array[leftPtr];
                    leftPtr++;
                }
                else
                {
                    temporaryArray[i] = array[rightPtr];
                    rightPtr++;
                }
            }
            for (int i = 0; i < length; i++)
                array[i + start] = temporaryArray[i];
        }

        static void MergeSortFunction(List<T> array, int start, int end)
        {
            if (start == end) return;
            var middle = (start + end) / 2;
            MergeSortFunction(array, start, middle);
            MergeSortFunction(array, middle + 1, end);
            Merge(array, start, middle, end);

        }

        public static void MergeSortFunction(List<T> array)
        {
            if (array.Count == 0) throw new Exception("пустой массив!");
            temporaryArray = new T[array.Count];
            MergeSortFunction(array, 0, array.Count - 1);
        }
    }
}
