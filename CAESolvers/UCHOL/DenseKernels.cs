namespace CAESolvers
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Плотные ядра, из которых состоит горячий цикл суперузловой
    /// факторизации. Вынесены отдельно, потому что именно здесь проводится
    /// подавляющая часть времени: суперузловое разбиение существует ровно для
    /// того, чтобы работа сводилась к этим операциям над непрерывными
    /// участками памяти.
    ///
    /// Векторизация выполняется через <see cref="Vector{T}"/> — платформенно
    /// независимый SIMD: на x64 с AVX2 это четыре числа с двойной точностью за
    /// такт, на AVX-512 — восемь, на ARM (NEON) — два, и всё это без
    /// отдельных ветвей кода под каждую архитектуру.
    /// </summary>
    internal static class DenseKernels
    {
        /// <summary>
        /// target[targetOffset + t] -= source[sourceOffset + t] * factor,
        /// t = 0..length-1. Участки не пересекаются (это либо разные столбцы
        /// одной фронтальной матрицы, либо вообще разные массивы).
        /// </summary>
        public static unsafe void SubtractScaled(
            double[] target, int targetOffset, double[] source, int sourceOffset, int length, double factor)
        {
            int width = Vector<double>.Count;
            int t = 0;

            fixed (double* targetBase = target, sourceBase = source)
            {
                double* to = targetBase + targetOffset;
                double* from = sourceBase + sourceOffset;

                if (length >= width)
                {
                    var scale = new Vector<double>(factor);
                    for (; t <= length - width; t += width)
                    {
                        var current = Unsafe.Read<Vector<double>>(to + t);
                        var update = Unsafe.Read<Vector<double>>(from + t);
                        Unsafe.Write(to + t, current - update * scale);
                    }
                }

                for (; t < length; t++)
                    to[t] -= from[t] * factor;
            }
        }

        /// <summary>
        /// Умножает участок на скаляр: buffer[offset + t] *= factor.
        /// </summary>
        public static unsafe void Scale(double[] buffer, int offset, int length, double factor)
        {
            int width = Vector<double>.Count;
            int t = 0;

            fixed (double* bufferBase = buffer)
            {
                double* target = bufferBase + offset;

                if (length >= width)
                {
                    var scale = new Vector<double>(factor);
                    for (; t <= length - width; t += width)
                        Unsafe.Write(target + t, Unsafe.Read<Vector<double>>(target + t) * scale);
                }

                for (; t < length; t++)
                    target[t] *= factor;
            }
        }

        /// <summary>
        /// Вычитает накопленный векторный регистр из непрерывного участка
        /// памяти: target[0 .. Count-1] -= value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SubtractVector(double* target, Vector<double> value)
        {
            Unsafe.Write(target, Unsafe.Read<Vector<double>>(target) - value);
        }

        /// <summary>
        /// Скалярное произведение двух непрерывных участков одинаковой длины.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(double[] left, int leftOffset, double[] right, int rightOffset, int length)
        {
            int width = Vector<double>.Count;
            int t = 0;
            double sum = 0.0;

            if (length >= width)
            {
                var accumulator = Vector<double>.Zero;
                for (; t <= length - width; t += width)
                {
                    accumulator += new Vector<double>(left, leftOffset + t)
                                 * new Vector<double>(right, rightOffset + t);
                }

                sum = Vector.Dot(accumulator, Vector<double>.One);
            }

            for (; t < length; t++)
                sum += left[leftOffset + t] * right[rightOffset + t];

            return sum;
        }

    }
}
