namespace TaskSolverCore
{
    /// <summary>
    /// Результат физической проверки после очередного решения линейной системы.
    /// </summary>
    public sealed class TaskIterationResult
    {
        public TaskIterationResult(
            bool converged,
            bool canContinue,
            bool matrixMustBeUpdated,
            double solutionChange,
            double solutionMaximum,
            double physicalResidual)
        {
            Converged = converged;
            CanContinue = canContinue;
            MatrixMustBeUpdated = matrixMustBeUpdated;
            SolutionChange = solutionChange;
            SolutionMaximum = solutionMaximum;
            PhysicalResidual = physicalResidual;
        }

        public bool Converged { get; }

        public bool CanContinue { get; }

        public bool MatrixMustBeUpdated { get; }

        public double SolutionChange { get; }

        public double SolutionMaximum { get; }

        public double PhysicalResidual { get; }
    }
}
