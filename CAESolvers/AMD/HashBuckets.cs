namespace CAESolvers
{
    /// <summary>
    /// Хеш-таблица с цепочками для поиска неразличимых переменных
    /// (см. <see cref="ApproximateMinimumDegreeOrdering"/>).
    /// head[bucket] — первая вершина цепочки; next[i]/prev[i] — соседние
    /// вершины той же цепочки, что и i (цепочка двусвязная — см.
    /// <see cref="Unlink"/>).
    ///
    /// За один шаг исключения pivot'а в таблицу попадает не больше
    /// вершин, чем было в Lme (обычно много меньше n), поэтому полная
    /// очистка head[] на n элементов на каждом шаге была бы значительно
    /// дороже самой вставки. Вместо этого used[] хранит, какие корзины
    /// реально были задействованы в текущем шаге — <see cref="ClearBucket"/>
    /// освобождает их точечно.
    ///
    /// Используется только <see cref="ApproximateMinimumDegreeOrdering"/>,
    /// поэтому internal. Маркер «нет вершины» — локальная константа (см.
    /// <see cref="DegreeBuckets"/> — та же логика: единственный вызывающий
    /// всегда передавал бы одно и то же значение, так что выносить его
    /// параметром смысла не было).
    /// </summary>
    internal sealed class HashBuckets
    {
        private const int Empty = -1;

        private readonly int[] head;
        private readonly int[] next;
        private readonly int[] prev;
        private readonly int[] used;
        private readonly int n;
        private int usedCount;

        public HashBuckets(int n)
        {
            this.n = n;
            head = new int[n];
            next = new int[n];
            prev = new int[n];
            used = new int[n];

            for (int i = 0; i < n; i++)
            {
                head[i] = Empty;
                next[i] = Empty;
                prev[i] = Empty;
            }
        }

        /// <summary>Начинает новый шаг — забывает список задействованных корзин.</summary>
        public void BeginRound() => usedCount = 0;

        /// <summary>Добавляет вершину i в цепочку по хешу (может быть любым long, суммой участников).</summary>
        public void Add(long hash, int i)
        {
            int bucket = (int)(hash % n);
            int oldHead = head[bucket];

            if (oldHead == Empty)
                used[usedCount++] = bucket;
            else
                prev[oldHead] = i;

            next[i] = oldHead;
            prev[i] = Empty;
            head[bucket] = i;
        }

        public int UsedCount => usedCount;
        public int UsedBucketAt(int index) => used[index];

        public int First(int bucket) => head[bucket];
        public int NextOf(int i) => next[i];

        /// <summary>
        /// Исключает вершину i из её цепочки за O(1) через собственные
        /// prev[i]/next[i] — без обхода цепочки заново и без ручного
        /// отслеживания «предыдущей» вершины вызывающим кодом.
        ///
        /// Требует, чтобы i не была головой цепочки (prev[i] != Empty):
        /// единственный вызывающий (поиск неразличимых переменных) всегда
        /// вызывает Unlink только для вершин, обнаруженных строго после
        /// головы цепочки, так что это ограничение никогда не нарушается,
        /// и обновлять head[] здесь не нужно.
        /// </summary>
        public void Unlink(int i)
        {
            int p = prev[i];
            int after = next[i];

            next[p] = after;
            if (after != Empty)
                prev[after] = p;
        }

        /// <summary>
        /// Освобождает корзину для следующего шага. Вызывается вызывающим
        /// кодом после того, как цепочка обработана полностью — сам класс
        /// не знает, когда обход завершён.
        /// </summary>
        public void ClearBucket(int bucket) => head[bucket] = Empty;
    }
}
