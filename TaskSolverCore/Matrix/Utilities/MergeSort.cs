using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Matrix.Utilities
{
 

    public static class MergeSort
    {
        static int[] temporaryArray;

        static void Merge(List<int> array, int start, int middle, int end)
        {
            var leftPtr = start;
            var rightPtr = middle + 1;
            var length = end - start + 1;
            for (int i = 0; i < length; i++)
            {
                if (rightPtr > end || (leftPtr <= middle && array[leftPtr] < array[rightPtr]))
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

        static void MergeSortFunction(List<int> array, int start, int end)
        {
            if (start == end) return;
            var middle = (start + end) / 2;
            MergeSortFunction(array, start, middle);
            MergeSortFunction(array, middle + 1, end);
            Merge(array, start, middle, end);

        }

        public static void MergeSortFunction(List<int> array)
        {
            temporaryArray = new int[array.Count];
            MergeSortFunction(array, 0, array.Count - 1);
        }
    }
}
