namespace CAESolvers
{
    using System;

    /// <summary>
    /// Метод сопряжённых градиентов (Conjugate Gradient) с диагональным
    /// (Якоби) предобуславливанием для симметричных положительно определённых
    /// систем A x = b, где A — <see cref="SymmetricCSRMatrix"/>
    /// (типичный случай в проекте — глобальная матрица жёсткости МКЭ:
    /// упругость, теплопроводность и т.п.).
    ///
    /// Предобуславливатель M = diag(A) применяется неявно на каждой
    /// итерации — покомпонентным делением невязки на диагональ через
    /// <see cref="SymmetricCSRMatrix.GetDiagonal"/> (доступ к диагонали
    /// за O(1), как и задумано в этом классе для итерационных решателей).
    /// Предобуславливание можно отключить через <see cref="UsePreconditioner"/>
    /// = false — тогда метод вырождается в классический CG без
    /// предобуславливания.
    ///
    /// Критерий останова — относительная норма невязки:
    /// ||b - A x_k|| / ||b|| &lt;= RelativeTolerance. Лимит итераций задаётся
    /// через MaxIterations; значение 0 (по умолчанию для этого решателя)
    /// означает «взять равным числу неизвестных» — в точной арифметике CG
    /// сходится не более чем за n итераций.
    ///
    /// Решатель не выделяет память в цикле итераций: все рабочие векторы
    /// (невязка, предобусловленная невязка, направление спуска и
    /// произведение A * p) выделяются один раз за вызов <see cref="Solve"/>
    /// и переиспользуются, а умножение матрицы на вектор идёт через
    /// перегрузку <see cref="SymmetricCSRMatrix.Multiply(double[], double[])"/>,
    /// пишущую результат в готовый буфер. На десятках тысяч итераций
    /// аллокация вектора результата на каждое умножение сопоставима по
    /// стоимости с самим умножением и вдобавок нагружает сборщик мусора.
    ///
    /// Матрица A должна быть симметричной положительно определённой — на
    /// этом основана сходимость метода. Сам решатель не проверяет это
    /// заранее (проверка дорога), но обнаруживает явное нарушение
    /// предположения по ходу итераций (p^T A p &lt;= 0) и в этом случае
    /// бросает исключение, а не возвращает бессмысленный результат.
    /// </summary>
    public class ConjugateGradientGaussPreSolver
        : IterativeSolver<SymmetricCSRMatrix, IterativeSolverResult>
    {
        /// <summary>
        /// Включает диагональное (Якоби) предобуславливание. По умолчанию
        /// включено: для типичных плохо масштабированных матриц жёсткости
        /// оно заметно ускоряет сходимость почти бесплатно, так как
        /// диагональ уже доступна за O(1).
        /// </summary>
        public bool UsePreconditioner { get; set; } = true;

        protected override double[] SolveCore(SymmetricCSRMatrix matrix, double[] rightHandSide)
        {
            return SolveWithInitialGuess(matrix, rightHandSide, null);
        }

        /// <summary>
        /// Решает A x = b методом CG (с якоби-предобуславливанием, если
        /// <see cref="UsePreconditioner"/>). Если задан initialGuess, он
        /// используется как начальное приближение x0 (иначе x0 = 0).
        /// </summary>
        public double[] SolveWithInitialGuess(LinearSystem system, double[]? initialGuess)
        {
            var matrix = GetMatrix(system);
            return SolveWithInitialGuess(matrix, system.RightHandSide, initialGuess);
        }

        private double[] SolveWithInitialGuess(SymmetricCSRMatrix matrix, double[] rightHandSide, double[]? initialGuess)
        {
            LastResult = null;

            ValidateCommonArguments();

            var b = rightHandSide;

            var n = matrix.Size;

            if (initialGuess != null)
            {
                if (initialGuess.Length != n)
                    throw new ArgumentException(
                        $"The initial guess length {initialGuess.Length} does not match the matrix size {n}.");
            }

            var x = initialGuess != null
                ? (double[])initialGuess.Clone()
                : new double[n];

            if (n == 0)
                return Complete(new IterativeSolverResult(x, 0, true, 0.0));

            var residualThreshold = RelativeTolerance * CalculateNorm(b);

            // Рабочие векторы на весь вызов: невязка r, предобусловленная
            // невязка z, направление спуска p и произведение A * p.
            var r = new double[n];
            var z = new double[n];
            var p = new double[n];
            var Ap = new double[n];

            // До входа в цикл Ap служит буфером под A * x0.
            matrix.Multiply(x, Ap);
            for (var i = 0; i < n; i++)
                r[i] = b[i] - Ap[i];

            var rNorm = CalculateNorm(r);

            if (rNorm <= residualThreshold)
                return Complete(new IterativeSolverResult(x, 0, true, rNorm));

            ApplyPreconditioner(matrix, r, z);
            Array.Copy(z, p, n);
            var rzOld = Dot(r, z);

            var maxIterations = MaxIterations > 0 ? MaxIterations : n;

            for (var iteration = 1; iteration <= maxIterations; iteration++)
            {
                matrix.Multiply(p, Ap);
                var pAp = Dot(p, Ap);

                if (pAp <= 0.0)
                    throw new InvalidOperationException(
                        "p^T A p <= 0. The matrix is not positive definite, so the CG method cannot be applied.");

                var alpha = rzOld / pAp;

                for (var i = 0; i < n; i++)
                {
                    x[i] += alpha * p[i];
                    r[i] -= alpha * Ap[i];
                }

                rNorm = CalculateNorm(r);
                if (rNorm <= residualThreshold)
                    return Complete(
                        new IterativeSolverResult(x, iteration, true, rNorm));

                ApplyPreconditioner(matrix, r, z);
                var rzNew = Dot(r, z);
                var beta = rzNew / rzOld;

                for (var i = 0; i < n; i++)
                    p[i] = z[i] + beta * p[i];

                rzOld = rzNew;
            }

            return Complete(
                new IterativeSolverResult(x, maxIterations, false, rNorm));
        }

        private double[] Complete(IterativeSolverResult result)
        {
            LastResult = result;
            return result.Solution;
        }

        /// <summary>
        /// Применяет якоби-предобуславливатель: z = D^-1 r, где D — диагональ
        /// матрицы. При UsePreconditioner = false просто копирует r в z
        /// (предобуславливатель = единичная матрица). Результат пишется в
        /// готовый буфер z, чтобы не выделять вектор на каждой итерации.
        /// </summary>
        private void ApplyPreconditioner(
            SymmetricCSRMatrix matrix, double[] r, double[] z)
        {
            if (!UsePreconditioner)
            {
                Array.Copy(r, z, r.Length);
                return;
            }

            for (var i = 0; i < r.Length; i++)
            {
                var d = matrix.GetDiagonal(i);
                z[i] = Math.Abs(d) > 1e-300 ? r[i] / d : r[i];
            }
        }

        private double Dot(double[] a, double[] b)
        {
            var sum = 0.0;
            for (var i = 0; i < a.Length; i++)
                sum += a[i] * b[i];
            return sum;
        }
    }
}
