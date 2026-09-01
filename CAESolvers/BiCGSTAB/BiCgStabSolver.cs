namespace CAESolvers
{
    /// <summary>
    /// Стабилизированный метод бисопряжённых градиентов для общих квадратных
    /// CSR-матриц с необязательным ILU(0)-предобуславливанием.
    /// </summary>
    public sealed class BiCgStabSolver : IterativeSolver<CSRMatrix, IterativeSolverResult>
    {
        /// <summary>Включает построение и применение ILU(0). По умолчанию включено.</summary>
        public bool UsePreconditioner { get; set; } = true;

        /// <summary>Порог обнаружения численного breakdown алгоритма.</summary>
        public double BreakdownTolerance { get; set; } = 1e-30;

        /// <inheritdoc/>
        public override double[] Solve(LinearSystem<CSRMatrix> system)
        {
            return Solve(system, null, null);
        }

        /// <summary>Решает систему с заданным начальным приближением.</summary>
        public double[] Solve(LinearSystem<CSRMatrix> system, double[]? initialGuess)
        {
            return Solve(system, initialGuess, null);
        }

        /// <summary>
        /// Решает систему с возможностью переиспользовать готовую ILU(0)-факторизацию.
        /// Переданный предобуславливатель имеет приоритет над UsePreconditioner.
        /// </summary>
        public double[] Solve(LinearSystem<CSRMatrix> system, double[]? initialGuess, Ilu0Preconditioner? preconditioner)
        {
            LastResult = null;
            ValidateCommonArguments(system);
            ValidateBreakdownTolerance();

            var matrix = system.Matrix;
            var rightHandSide = system.RightHandSide;

            if (matrix.RowCount != matrix.ColumnCount)
                throw new ArgumentException("BiCGStab применим только к квадратной матрице.", nameof(system));

            var size = matrix.RowCount;
            if (initialGuess != null && initialGuess.Length != size)
                throw new ArgumentException("Размер начального приближения не соответствует размеру матрицы.", nameof(initialGuess));

            if (preconditioner != null && !ReferenceEquals(preconditioner.Matrix, matrix))
                throw new ArgumentException("Предобуславливатель построен для другой матрицы.", nameof(preconditioner));

            var x = initialGuess != null ? (double[])initialGuess.Clone() : new double[size];
            if (size == 0)
                return Complete(new IterativeSolverResult(x, 0, true, 0.0));

            var residual = new double[size];
            var shadowResidual = new double[size];
            var direction = new double[size];
            var preconditionedDirection = new double[size];
            var matrixDirection = new double[size];
            var intermediateResidual = new double[size];
            var preconditionedIntermediate = new double[size];
            var matrixIntermediate = new double[size];
            var workspace = new double[size];

            Multiply(matrix, x, matrixDirection);
            for (var index = 0; index < size; index++)
                residual[index] = rightHandSide[index] - matrixDirection[index];

            Array.Copy(residual, shadowResidual, size);

            var rightHandSideNorm = CalculateNorm(rightHandSide);
            var residualThreshold = RelativeTolerance * (rightHandSideNorm > 0.0 ? rightHandSideNorm : 1.0);
            var residualNorm = CalculateNorm(residual);

            if (residualNorm <= residualThreshold)
                return Complete(new IterativeSolverResult(x, 0, true, residualNorm));

            var activePreconditioner = preconditioner;
            if (activePreconditioner == null && UsePreconditioner)
                activePreconditioner = new Ilu0Preconditioner(matrix, BreakdownTolerance);

            var rhoPrevious = 1.0;
            var alpha = 1.0;
            var omega = 1.0;
            var maxIterations = MaxIterations > 0 ? MaxIterations : size;

            for (var iteration = 1; iteration <= maxIterations; iteration++)
            {
                var rho = Dot(shadowResidual, residual);
                EnsureNotBreakdown(rho, "rho", iteration);

                if (iteration == 1)
                {
                    Array.Copy(residual, direction, size);
                }
                else
                {
                    EnsureNotBreakdown(omega, "omega", iteration);
                    var beta = rho / rhoPrevious * (alpha / omega);
                    for (var index = 0; index < size; index++)
                        direction[index] = residual[index] + beta * (direction[index] - omega * matrixDirection[index]);
                }

                ApplyPreconditioner(activePreconditioner, direction, preconditionedDirection, workspace);
                Multiply(matrix, preconditionedDirection, matrixDirection);

                var alphaDenominator = Dot(shadowResidual, matrixDirection);
                EnsureNotBreakdown(alphaDenominator, "(r0, A M^-1 p)", iteration);
                alpha = rho / alphaDenominator;

                for (var index = 0; index < size; index++)
                    intermediateResidual[index] = residual[index] - alpha * matrixDirection[index];

                var intermediateNorm = CalculateNorm(intermediateResidual);
                if (intermediateNorm <= residualThreshold)
                {
                    for (var index = 0; index < size; index++)
                        x[index] += alpha * preconditionedDirection[index];

                    return Complete(new IterativeSolverResult(x, iteration, true, intermediateNorm));
                }

                ApplyPreconditioner(activePreconditioner, intermediateResidual, preconditionedIntermediate, workspace);
                Multiply(matrix, preconditionedIntermediate, matrixIntermediate);

                var omegaDenominator = Dot(matrixIntermediate, matrixIntermediate);
                EnsureNotBreakdown(omegaDenominator, "(A M^-1 s, A M^-1 s)", iteration);
                omega = Dot(matrixIntermediate, intermediateResidual) / omegaDenominator;
                EnsureNotBreakdown(omega, "omega", iteration);

                for (var index = 0; index < size; index++)
                {
                    x[index] += alpha * preconditionedDirection[index] + omega * preconditionedIntermediate[index];
                    residual[index] = intermediateResidual[index] - omega * matrixIntermediate[index];
                }

                residualNorm = CalculateNorm(residual);
                if (residualNorm <= residualThreshold)
                    return Complete(new IterativeSolverResult(x, iteration, true, residualNorm));

                rhoPrevious = rho;
            }

            return Complete(new IterativeSolverResult(x, maxIterations, false, residualNorm));
        }

        private double[] Complete(IterativeSolverResult result)
        {
            LastResult = result;
            return result.Solution;
        }

        private static void ApplyPreconditioner(Ilu0Preconditioner? preconditioner, double[] source, double[] result, double[] workspace)
        {
            if (preconditioner == null)
                Array.Copy(source, result, source.Length);
            else
                preconditioner.Apply(source, result, workspace);
        }

        private static void Multiply(CSRMatrix matrix, double[] vector, double[] result)
        {
            var rowPointers = matrix.RowPointers;
            var columnIndices = matrix.ColumnIndices;
            var values = matrix.Values;

            for (var row = 0; row < matrix.RowCount; row++)
            {
                var sum = 0.0;
                for (var position = rowPointers[row]; position < rowPointers[row + 1]; position++)
                    sum += values[position] * vector[columnIndices[position]];

                result[row] = sum;
            }
        }

        private static double Dot(double[] left, double[] right)
        {
            var result = 0.0;
            for (var index = 0; index < left.Length; index++)
                result += left[index] * right[index];

            return result;
        }

        private void ValidateBreakdownTolerance()
        {
            if (!double.IsFinite(BreakdownTolerance) || BreakdownTolerance <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(BreakdownTolerance));
        }

        private void EnsureNotBreakdown(double value, string valueName, int iteration)
        {
            if (!double.IsFinite(value) || Math.Abs(value) < BreakdownTolerance)
                throw new InvalidOperationException($"BiCGStab breakdown: {valueName} равен нулю или не является конечным на итерации {iteration}.");
        }
    }
}
