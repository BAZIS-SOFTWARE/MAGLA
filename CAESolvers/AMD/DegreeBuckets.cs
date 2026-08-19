namespace CAESolvers
{
    using System;

    /// <summary>
    /// Списки вершин по значению степени (bucket sort): head[deg] —
    /// первая вершина с данной степенью, next/last связывают вершины
    /// внутри списка одной корзины в двусвязный список. minDegree —
    /// наименьшая степень, для которой список гарантированно может быть
    /// непустым (используется как отправная точка поиска в
    /// <see cref="PopMinimum"/>, чтобы не сканировать корзины с нуля на
    /// каждом шаге).
    ///
    /// Сам класс не хранит значения степеней — только связи внутри
    /// корзин. Текущая степень каждой вершины (индекс корзины, в которой
    /// вершина должна лежать) — обязанность вызывающего кода: при
    /// изменении степени сначала нужно позвать <see cref="Remove"/> со
    /// старым значением, затем <see cref="Insert"/> с новым.
    ///
    /// Используется только <see cref="ApproximateMinimumDegreeOrdering"/>,
    /// поэтому internal — за пределы сборки эта деталь реализации не
    /// выходит. Маркер «нет вершины» — локальная константа, а не входной
    /// параметр: у класса ровно один вызывающий, и он всегда передавал бы
    /// сюда одно и то же значение (-1), так что параметр не даёт реальной
    /// гибкости, только лишнюю сущность.
    /// </summary>
    internal sealed class DegreeBuckets
    {
        private const int Empty = -1;

        private readonly int[] head;
        private readonly int[] next;
        private readonly int[] last;
        private int minDegree;

        public DegreeBuckets(int n)
        {
            head = new int[n + 1];
            next = new int[n];
            last = new int[n];

            for (int i = 0; i < n; i++)
            {
                next[i] = Empty;
                last[i] = Empty;
            }

            for (int d = 0; d <= n; d++)
                head[d] = Empty;

            minDegree = 0;
        }

        /// <summary>Сбрасывает нижнюю границу поиска минимума в 0.</summary>
        public void ResetMinDegree() => minDegree = 0;

        public void Insert(int i, int deg)
        {
            int following = head[deg];
            if (following != Empty)
                last[following] = i;

            next[i] = following;
            last[i] = Empty;
            head[deg] = i;

            if (deg < minDegree)
                minDegree = deg;
        }

        /// <summary>
        /// Убирает вершину i из корзины. deg должна совпадать со
        /// значением, с которым вершина была вставлена (или в которое
        /// перенесена последним вызовом Insert) — сам класс это не
        /// проверяет, актуальную степень хранит вызывающий код.
        /// </summary>
        public void Remove(int i, int deg)
        {
            int previous = last[i];
            int following = next[i];

            if (following != Empty)
                last[following] = previous;

            if (previous != Empty)
                next[previous] = following;
            else
                head[deg] = following;
        }

        /// <summary>
        /// Извлекает и убирает вершину с наименьшей непустой степенью,
        /// не больше maxDegree. Бросает, если такой вершины нет — на
        /// непустом графе это внутренняя ошибка вызывающего кода.
        /// </summary>
        public int PopMinimum(int maxDegree)
        {
            for (int deg = minDegree; deg <= maxDegree; deg++)
            {
                int candidate = head[deg];
                if (candidate == Empty)
                    continue;

                minDegree = deg;
                int following = next[candidate];
                if (following != Empty)
                    last[following] = Empty;
                head[deg] = following;
                return candidate;
            }

            throw new InvalidOperationException(
                "AMD: не найдена вершина для исключения при непустом графе — внутренняя ошибка.");
        }
    }
}
