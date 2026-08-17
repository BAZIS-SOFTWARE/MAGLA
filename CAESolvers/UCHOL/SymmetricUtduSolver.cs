namespace CAESolvers
{
    using System;

    /// <summary>
    /// Прямой решатель СЛАУ A x = b для симметричных матриц, хранящихся в
    /// <see cref="SymmetricCSRMatrix"/>, на основе разложения
    /// A = U^T D U (единичная верхняя треугольная U, диагональная D).
    /// Ориентирован на матрицы жёсткости МКЭ в сотни тысяч уравнений.
    ///
    /// Чем он отличается от <see cref="ConjugateGradientGaussPreSolver"/>. Прямой
    /// метод даёт решение за фиксированное число операций, не зависящее от
    /// обусловленности: там, где итерационный решатель на плохо
    /// обусловленной задаче (тонкостенные конструкции, большой разброс
    /// жёсткостей, почти несжимаемые материалы) сходится за десятки тысяч
    /// итераций или не сходится вовсе, прямой отработает предсказуемо. Кроме
    /// того, дорогая часть работы — факторизация — делается один раз, после
    /// чего каждая дополнительная правая часть стоит два треугольных решения,
    /// то есть в сотни раз дешевле. Плата — память: множитель существенно
    /// плотнее исходной матрицы, и на трёхмерных задачах именно память, а не
    /// время, обычно становится ограничением. Оценить её можно заранее, до
    /// численной фазы, через <see cref="Analyze"/>.
    ///
    /// Работа делится на три фазы, и разделение здесь не формальное — оно
    /// определяет, как решателем правильно пользоваться:
    /// <list type="number">
    /// <item><see cref="Analyze"/> — символьная фаза: переупорядочивание для
    /// снижения заполнения, дерево исключений, суперузловое разбиение. Зависит
    /// только от структуры разреженности.</item>
    /// <item><see cref="Factorize"/> — численная фаза: собственно U и D.
    /// Распараллелена по ядрам процессора.</item>
    /// <item><see cref="UtduNumericFactorization.Solve"/> — треугольные
    /// подстановки для конкретной правой части.</item>
    /// </list>
    /// В нелинейном или многовариантном расчёте символьную фазу выполняют один
    /// раз и переиспользуют: структура матрицы жёсткости от итерации к итерации
    /// не меняется, а меняются только значения.
    ///
    /// Потокобезопасность: экземпляр решателя не хранит состояния между
    /// вызовами и может использоваться повторно;
    /// <see cref="UtduSymbolicFactorization"/> и
    /// <see cref="UtduNumericFactorization"/> неизменяемы после построения, и
    /// решать с одним множителем разные правые части можно параллельно.
    /// </summary>
    public class SymmetricUtduSolver : ISymmetricLinearSolver
    {
        /// <summary>
        /// Создаёт решатель с настройками по умолчанию (AMD-переупорядочивание,
        /// параллелизм по числу логических ядер, регуляризация диагонали
        /// разрешена).
        /// </summary>
        public SymmetricUtduSolver()
            : this(new UtduSolverOptions())
        {
        }

        public SymmetricUtduSolver(UtduSolverOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Настройки решателя.</summary>
        public UtduSolverOptions Options { get; }

        /// <summary>
        /// Символьная фаза. Результат зависит только от структуры
        /// разреженности матрицы и может быть переиспользован для любого числа
        /// численных факторизаций.
        /// </summary>
        public UtduSymbolicFactorization Analyze(SymmetricCSRMatrix matrix)
        {
            if (matrix == null)
                throw new ArgumentNullException(nameof(matrix));

            return UtduSymbolicFactorization.Analyze(matrix, Options);
        }

        /// <summary>
        /// Численная факторизация. Если символьная факторизация не передана,
        /// она вычисляется внутри; при повторных факторизациях матрицы с
        /// неизменной структурой её следует передавать явно — это исключает
        /// самую дорогую по накладным расходам часть работы.
        /// </summary>
        public UtduNumericFactorization Factorize(
            SymmetricCSRMatrix matrix, UtduSymbolicFactorization? symbolic = null)
        {
            if (matrix == null)
                throw new ArgumentNullException(nameof(matrix));

            if (symbolic == null)
                symbolic = Analyze(matrix);
            else
                symbolic.EnsureCompatible(matrix);

            return SupernodalMultifrontalFactorizer.Factorize(symbolic, matrix, Options);
        }

        /// <summary>
        /// Полное решение A x = b за один вызов: символьная фаза, численная
        /// факторизация и подстановки. Удобно для разового расчёта; если
        /// правых частей несколько или матрица будет пересобираться, дешевле
        /// вызвать <see cref="Analyze"/> и <see cref="Factorize"/> явно и
        /// решать по готовому множителю.
        /// </summary>
        public double[] Solve(SymmetricCSRMatrix matrix, double[] rightHandSide)
        {
            if (matrix == null)
                throw new ArgumentNullException(nameof(matrix));
            if (rightHandSide == null)
                throw new ArgumentNullException(nameof(rightHandSide));

            if (rightHandSide.Length != matrix.Size)
                throw new ArgumentException(
                    $"Размер вектора правой части {rightHandSide.Length} не соответствует размеру матрицы {matrix.Size}");

            return Factorize(matrix).Solve(rightHandSide);
        }
    }
}
