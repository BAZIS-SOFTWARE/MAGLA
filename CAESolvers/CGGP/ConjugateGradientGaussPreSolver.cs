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
    /// Матрица A должна быть симметричной положительно определённой — на
    /// этом основана сходимость метода. Сам решатель не проверяет это
    /// заранее (проверка дорога), но обнаруживает явное нарушение
    /// предположения по ходу итераций (p^T A p &lt;= 0) и в этом случае
    /// бросает исключение, а не возвращает бессмысленный результат.
    /// </summary>
    public class ConjugateGradientGaussPreSolver : ISymmetricLinearSolver
    {
        /// <summary>
        /// Критерий останова по относительной норме невязки:
        /// ||b - A x_k|| / ||b|| &lt;= Tolerance. Если ||b|| пренебрежимо
        /// мала (правая часть ~ 0), используется абсолютная норма невязки.
        /// </summary>
        public double Tolerance { get; set; } = 1e-8;

        /// <summary>
        /// Максимум итераций. Значение 0 (по умолчанию) означает "взять
        /// равным числу неизвестных" — в точной арифметике CG сходится не
        /// более чем за n итераций.
        /// </summary>
        public int MaxIterations { get; set; } = 0;

        /// <summary>
        /// Включает диагональное (Якоби) предобуславливание. По умолчанию
        /// включено: для типичных плохо масштабированных матриц жёсткости
        /// оно заметно ускоряет сходимость почти бесплатно, так как
        /// диагональ уже доступна за O(1).
        /// </summary>
        public bool UsePreconditioner { get; set; } = true;

        /// <summary>
        /// Решает A x = b методом CG (с якоби-предобуславливанием, если
        /// <see cref="UsePreconditioner"/>). Если задан initialGuess, он
        /// используется как начальное приближение x0 (иначе x0 = 0).
        /// </summary>
        public IterativeSolverResult Solve(SymmetricCSRMatrix matrix, double[] b, double[]? initialGuess = null)
        {
            if (matrix == null)
                throw new ArgumentNullException(nameof(matrix));
            if (b == null)
                throw new ArgumentNullException(nameof(b));

            int n = matrix.Size;
            if (b.Length != n)
                throw new ArgumentException(
                    $"Размер вектора правой части {b.Length} не соответствует размеру матрицы {n}");

            double[] x;
            if (initialGuess != null)
            {
                if (initialGuess.Length != n)
                    throw new ArgumentException(
                        $"Размер начального приближения {initialGuess.Length} не соответствует размеру матрицы {n}");

                x = (double[])initialGuess.Clone();
            }
            else
            {
                x = new double[n];
            }

            if (n == 0)
                return new IterativeSolverResult(x, 0, true, 0.0);

            double bNorm = Norm(b);
            double residualThreshold = Tolerance * (bNorm > 1e-300 ? bNorm : 1.0);

            double[] r = Subtract(b, matrix.Multiply(x));
            double rNorm = Norm(r);

            if (rNorm <= residualThreshold)
                return new IterativeSolverResult(x, 0, true, rNorm);

            double[] z = ApplyPreconditioner(matrix, r);
            double[] p = (double[])z.Clone();
            double rzOld = Dot(r, z);

            int maxIterations = MaxIterations > 0 ? MaxIterations : n;

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                double[] Ap = matrix.Multiply(p);
                double pAp = Dot(p, Ap);

                if (pAp <= 0.0)
                    throw new InvalidOperationException(
                        "p^T A p <= 0 — матрица не является положительно определённой, метод CG неприменим.");

                double alpha = rzOld / pAp;

                for (int i = 0; i < n; i++)
                {
                    x[i] += alpha * p[i];
                    r[i] -= alpha * Ap[i];
                }

                rNorm = Norm(r);
                if (rNorm <= residualThreshold)
                    return new IterativeSolverResult(x, iteration, true, rNorm);

                z = ApplyPreconditioner(matrix, r);
                double rzNew = Dot(r, z);
                double beta = rzNew / rzOld;

                for (int i = 0; i < n; i++)
                    p[i] = z[i] + beta * p[i];

                rzOld = rzNew;
            }

            return new IterativeSolverResult(x, maxIterations, false, rNorm);
        }

        /// <summary>
        /// Реализация общего контракта решателя. Использует нулевое начальное
        /// приближение и возвращает только сошедшееся решение.
        /// </summary>
        /// <exception cref="SolverConvergenceException">
        /// Заданная точность не достигнута за разрешённое число итераций.
        /// </exception>
        double[] ISymmetricLinearSolver.Solve(
            SymmetricCSRMatrix matrix, double[] rightHandSide)
        {
            IterativeSolverResult result = Solve(matrix, rightHandSide);
            if (!result.Converged)
                throw new SolverConvergenceException(result.Iterations, result.ResidualNorm);

            return result.Solution;
        }

        /// <summary>
        /// Применяет якоби-предобуславливатель: z = D^-1 r, где D — диагональ
        /// матрицы. При UsePreconditioner = false возвращает копию r
        /// (предобуславливатель = единичная матрица).
        /// </summary>
        private double[] ApplyPreconditioner(SymmetricCSRMatrix matrix, double[] r)
        {
            if (!UsePreconditioner)
                return (double[])r.Clone();

            int n = r.Length;
            var z = new double[n];
            for (int i = 0; i < n; i++)
            {
                double d = matrix.GetDiagonal(i);
                z[i] = Math.Abs(d) > 1e-300 ? r[i] / d : r[i];
            }

            return z;
        }

        private static double Dot(double[] a, double[] b)
        {
            double sum = 0.0;
            for (int i = 0; i < a.Length; i++)
                sum += a[i] * b[i];
            return sum;
        }

        private static double Norm(double[] v) => Math.Sqrt(Dot(v, v));

        private static double[] Subtract(double[] a, double[] b)
        {
            var result = new double[a.Length];
            for (int i = 0; i < a.Length; i++)
                result[i] = a[i] - b[i];
            return result;
        }
    }
}
