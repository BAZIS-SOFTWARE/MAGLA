using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.MatrixSolvers
{
    /// <summary>
    /// IMatrixSolver
    /// </summary>
    public abstract class MatrixSolver
    {
        /// <summary>
        /// Name
        /// </summary>
        public string? Name { get; internal set; }
        /// <summary>
        /// SMP
        /// </summary>
        public bool SMP
        {
            get
            {
                if (ParallelOptions.MaxDegreeOfParallelism == 1)
                    return false;
                else
                    return true;
            }
        }

        /// <summary>
        /// ParallelOptions
        /// </summary>
        public ParallelOptions ParallelOptions { get; } =
            new ParallelOptions { MaxDegreeOfParallelism = 1 };
/// <inheritdoc/>

        public override string ToString()
        {
            return $"{Name},{ParallelOptions.MaxDegreeOfParallelism}";
        }
    }
}
