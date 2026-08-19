using System.Runtime.InteropServices;
using TaskSolverCore.Matrix;

namespace TaskSolverCore.MatrixSolvers
{
    public class BandGaussDirect : MatrixSolver
    {
        public BandGaussDirect(int processors) : this()
        {
            ParallelOptions.MaxDegreeOfParallelism = processors;
        }

        public BandGaussDirect()
        {

        }

        public Tuple<List<int>[], List<int>[]> U_Simbol_2(double[][] matrix, int bandWidth)
        {
            int n = matrix.Length - 1;

            // Символьное разложение (предсказание заполнения)
            for (int ii = 0; ii <= n; ii++)
            {
                int maxJj = Math.Min(bandWidth - 1, n - ii);

                for (int jj = 1; jj <= maxJj; jj++) // индекс колонны главного ряда
                {
                    if (matrix[ii][jj] != 0)
                    {
                        int row = ii + jj;
                        int maxKk = Math.Min(bandWidth - 1, n - row);

                        for (int kk = 0; kk <= maxKk; kk++) // индекс колонны вспомогательного ряда
                        {
                            int col = kk + jj;

                            // ВАЖНО: проверяем, что индексы существуют
                            if (col < matrix[ii].Length && kk < matrix[row].Length)
                            {
                                if (matrix[ii][col] != 0 && matrix[row][kk] == 0)
                                {
                                    matrix[row][kk] = 101010; // маркер нового элемента
                                }
                            }
                        }
                    }
                }
            }

            // Формируем списки индексов для верхней и нижней частей
            var u_simbMatr = new List<int>[matrix.GetLength(0)];
            var l_simbMatr = new List<int>[matrix.GetLength(0)];

            for (int i = 0; i < l_simbMatr.Length; i++)
                l_simbMatr[i] = new List<int>();

            for (int i = 0; i < matrix.Length; i++)
            {
                var list = new List<int>();
                for (int j = 0; j < matrix[i].Length; j++)
                {
                    if (matrix[i][j] != 0)
                    {
                        list.Add(j + i);
                        l_simbMatr[j + i].Add(i);
                    }
                    if (matrix[i][j] == 101010) matrix[i][j] = 0;
                }
                u_simbMatr[i] = list;
            }

            return new Tuple<List<int>[], List<int>[]>(u_simbMatr, l_simbMatr);
        }

        public Tuple<List<int>[], List<int>[]> U_Simbol_1(double[][] matrix, int bandWidth)
        {
            //var simbolMatrix = new int[matrix.GetLength(0)][]; // pointer to global nonzero upper diag indexes

            int n = matrix.Length - 1;
            var band = bandWidth - 1;
            var length = band;
            for (int ii = 0; ii <= n; ii++)
            {
                if (ii + band > n) 
                    length = n - ii;
                for (int jj = 1; jj <= length; jj++)
                {
                    if (matrix[ii][jj] != 0)
                    {
                        for (int kk = 0; kk <= length; kk++)
                        {
                            var col = kk + jj;
                            var row = jj + ii;
                            var b = matrix[ii][col];
                            var a = matrix[row][kk];
                            if (b != 0 && a == 0)
                            {
                                matrix[row][kk] = 101010;
                            }
                        }
                    }
                }
            }
            var u_simbMatr = new List<int>[matrix.GetLength(0)];
            var l_simbMatr = new List<int>[matrix.GetLength(0)];

            for (int i = 0; i < l_simbMatr.Length; i++)
                l_simbMatr[i] = new List<int>();

            for (int i = 0; i < matrix.Length; i++)
            {
                var list = new List<int>();
                for (int j = 0; j < matrix[i].Length; j++)
                {
                    if (matrix[i][j] != 0)
                    {
                        list.Add(j + i);
                        l_simbMatr[j + i].Add(i);
                    }
                    if (matrix[i][j] == 101010) matrix[i][j] = 0;
                }
                u_simbMatr[i] = list;
            }

            return new Tuple<List<int>[], List<int>[]>(u_simbMatr, l_simbMatr);
        }       

        public unsafe void U_Numeric(List<int>[] im, double[][] m, double[] y)
        {
            int n = y.Length - 1;

            GCHandle[] pinnedMatrixArray = new GCHandle[n + 1];
            double*[] ObjPtrMatrixArray = new double*[n + 1];

            GCHandle[] pinnedIndexArray = new GCHandle[n + 1];
            int*[] ObjPtrIndexArray = new int*[n + 1];
            double* ObjPtrY = stackalloc double[n + 1];

            for (int i = 0; i < n + 1; i++)
            {
                pinnedMatrixArray[i] = GCHandle.Alloc(m[i], GCHandleType.Pinned);
                pinnedIndexArray[i] = GCHandle.Alloc(im[i].ToArray(), GCHandleType.Pinned);
                ObjPtrY[i] = y[i];
            }

            for (int i = 0; i < n + 1; ++i)
            {
                // as you can see, this pointer will point to the first element of each array
                ObjPtrMatrixArray[i] = (double*)pinnedMatrixArray[i].AddrOfPinnedObject();
                ObjPtrIndexArray[i] = (int*)pinnedIndexArray[i].AddrOfPinnedObject();
            }

            for (int ii = 0; ii <= n; ii++)
            {

                /*
                * Compute the Schur complement
                * */
                var rowCount = im[ii].Count;
                if (!SMP)
                {
                    fixed (double** ObjPtrM = ObjPtrMatrixArray)
                    {
                        fixed (int** ObjPtrIM = ObjPtrIndexArray)
                        {
                            for (int jj = 1; jj < rowCount; jj++)
                            {
                                var lcol = ObjPtrIM[ii][jj] - ii; // global outdiag column index
                                var lrow = ObjPtrIM[ii][jj];

                                var colCount = im[lrow].Count;
                                var koeff = ObjPtrM[ii][lcol] / ObjPtrM[ii][0];

                                for (int kk = 0; kk < colCount; kk++)
                                {
                                    var sublcolA = ObjPtrIM[lrow][kk] - ii - lcol;  // колонна эл. который вычитают
                                    var sublcolP = ObjPtrIM[lrow][kk] - ii;  // колонна эл. из которого вычитают
                                    var mult = ObjPtrM[ii][sublcolP] * koeff;

                                    ObjPtrM[lrow][sublcolA] = ObjPtrM[lrow][sublcolA] - mult; // эл. из которого вычитают
                                }
                                ObjPtrY[lrow] = ObjPtrY[lrow] - (ObjPtrY[ii] * koeff);
                            }
                        }
                    }
                }
                else
                {

                    Parallel.For(1, rowCount, ParallelOptions, i =>
                    {
                        fixed (double** ObjPtrM = ObjPtrMatrixArray)
                        {
                            fixed (int** ObjPtrIM = ObjPtrIndexArray)
                            {
                                var lcol = ObjPtrIM[ii][i] - ii; // global outdiag column index
                                var lrow = ObjPtrIM[ii][i];

                                var colCount = im[lrow].Count;
                                var koeff = ObjPtrM[ii][lcol] / ObjPtrM[ii][0];

                                for (int kk = 0; kk < colCount; kk++)
                                {
                                    var sublcolA = ObjPtrIM[lrow][kk] - ii - lcol;  // колонна эл. который вычитают
                                    var sublcolP = ObjPtrIM[lrow][kk] - ii;  // колонна эл. из которого вычитают
                                    var mult = ObjPtrM[ii][sublcolP] * koeff;

                                    ObjPtrM[lrow][sublcolA] = ObjPtrM[lrow][sublcolA] - mult; // эл. из которого вычитают
                                }
                                ObjPtrY[lrow] = ObjPtrY[lrow] - (ObjPtrY[ii] * koeff);
                            }
                        }
                    });
                }
            }


            for (int i = 0; i < n + 1; ++i)
            {
                pinnedMatrixArray[i].Free();
                pinnedIndexArray[i].Free();
                y[i] = ObjPtrY[i];
            }
        }

        public unsafe void U_Numeric_1(List<int>[] im, double[][] m, double[] y)
        {
            int n = y.Length - 1;

            GCHandle[] pinnedMatrixArray = new GCHandle[n + 1];
            double*[] ObjPtrMatrixArray = new double*[n + 1];

            GCHandle[] pinnedIndexArray = new GCHandle[n + 1];
            int*[] ObjPtrIndexArray = new int*[n + 1];
            double* ObjPtrY = stackalloc double[n + 1];

            for (int i = 0; i < n + 1; i++)
            {
                pinnedMatrixArray[i] = GCHandle.Alloc(m[i], GCHandleType.Pinned);
                pinnedIndexArray[i] = GCHandle.Alloc(im[i].ToArray(), GCHandleType.Pinned);
                ObjPtrY[i] = y[i];
            }

            for (int i = 0; i < n + 1; ++i)
            {
                // as you can see, this pointer will point to the first element of each array
                ObjPtrMatrixArray[i] = (double*)pinnedMatrixArray[i].AddrOfPinnedObject();
                ObjPtrIndexArray[i] = (int*)pinnedIndexArray[i].AddrOfPinnedObject();
            }

            for (int ii = 0; ii <= n; ii++)
            {
                var rowCount = im[ii].Count;
                if (!SMP)
                {
                    fixed (double** ObjPtrM = ObjPtrMatrixArray)
                    {
                        fixed (int** ObjPtrIM = ObjPtrIndexArray)
                        {
                            for (int jj = 1; jj < rowCount; jj++)
                            {
                                var lcol = ObjPtrIM[ii][jj] - ii; // global outdiag column index
                                var lrow = ObjPtrIM[ii][jj];

                                var colCount = rowCount - jj;
                                var koeff = ObjPtrM[ii][lcol] / ObjPtrM[ii][0];

                                for (int kk = 0; kk < colCount; kk++)
                                {
                                    var sublcolA = ObjPtrIM[ii][jj + kk];  // колонна эл. который вычитают
                                    var mult = ObjPtrM[ii][sublcolA - ii] * koeff;

                                    var sublcolB = sublcolA - lrow;
                                    ObjPtrM[lrow][sublcolB] = ObjPtrM[lrow][sublcolB] - mult; // эл. из которого вычитают
                                }
                                ObjPtrY[lrow] = ObjPtrY[lrow] - (ObjPtrY[ii] * koeff);
                            }
                        }
                    }
                }
                else
                {
                    Parallel.For(1, rowCount, ParallelOptions, jj =>
                    {
                        fixed (double** ObjPtrM = ObjPtrMatrixArray)
                        {
                            fixed (int** ObjPtrIM = ObjPtrIndexArray)
                            {
                                var lcol = ObjPtrIM[ii][jj] - ii; // global outdiag column index
                                var lrow = ObjPtrIM[ii][jj];

                                var colCount = rowCount - jj;
                                var koeff = ObjPtrM[ii][lcol] / ObjPtrM[ii][0];

                                for (int kk = 0; kk < colCount; kk++)
                                {
                                    var sublcolA = ObjPtrIM[ii][jj + kk];  // колонна эл. который вычитают
                                    var mult = ObjPtrM[ii][sublcolA - ii] * koeff;

                                    var sublcolB = sublcolA - lrow;
                                    ObjPtrM[lrow][sublcolB] = ObjPtrM[lrow][sublcolB] - mult; // эл. из которого вычитают
                                }
                                ObjPtrY[lrow] = ObjPtrY[lrow] - (ObjPtrY[ii] * koeff);
                            }
                        }
                    });
                }
            }

            for (int i = 0; i < n + 1; ++i)
            {
                pinnedMatrixArray[i].Free();
                pinnedIndexArray[i].Free();
                y[i] = ObjPtrY[i];
            }
        } 


        public unsafe double[] Solve(double[][] m, List<int>[] im, double[] y)
        {
            int n = y.Length - 1;
            double* ObjPtrRes = stackalloc double[n + 1];
            double* ObjPtrY = stackalloc double[n + 1];
            var res = new double[n + 1];
            var sum = 0.0;

            for (int i = 0; i < n + 1; i++)
            {
                ObjPtrRes[i] = res[i];
                ObjPtrY[i] = y[i];
            }


            //int n = cm.GetLength(0) - 1;
            //var result = new float[cm.GetLength(0)];
            //float sum = 0;

            //Solve for x by using back substitution
            for (int i = n; i >= 0; i--)
            {
                sum = 0;
                var rowCount = im[i].Count;
                for (int j = 1; j < rowCount; j++)
                {
                    var ind = im[i][j];
                    sum = sum + (m[i][ind - i] * ObjPtrRes[ind]);
                }

                ObjPtrRes[i] = (ObjPtrY[i] - sum) / m[i][0];
            }
            for (int i = 0; i < n + 1; i++)
            {
                res[i] = ObjPtrRes[i];
            }
            return res;
        }
    }
}
