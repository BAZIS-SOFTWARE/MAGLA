namespace CAESolvers
{
    /// <summary>
    /// Состав суперпеременных: каждая живая суперпеременная i владеет
    /// односвязным списком исходных уравнений, которые она представляет
    /// (изначально — только себя, head[i] == tail[i] == i). При слиянии
    /// двух суперпеременных (массовое исключение, неразличимые
    /// переменные) их списки склеиваются за O(1) через <see cref="Append"/>
    /// без обхода — отсюда tail, который иначе не нужен для самого
    /// перечисления списка.
    ///
    /// Используется только <see cref="ApproximateMinimumDegreeOrdering"/>,
    /// поэтому internal. Маркер «нет вершины» — локальная константа (см.
    /// <see cref="DegreeBuckets"/> — та же логика: единственный вызывающий
    /// всегда передавал бы одно и то же значение).
    /// </summary>
    internal sealed class MemberLists
    {
        private const int Empty = -1;

        private readonly int[] head;
        private readonly int[] tail;
        private readonly int[] next;

        public MemberLists(int n)
        {
            head = new int[n];
            tail = new int[n];
            next = new int[n];

            for (int i = 0; i < n; i++)
            {
                head[i] = i;
                tail[i] = i;
                next[i] = Empty;
            }
        }

        /// <summary>Приписывает состав суперпеременной source к target.</summary>
        public void Append(int target, int source)
        {
            next[tail[target]] = head[source];
            tail[target] = tail[source];
        }

        public int First(int representative) => head[representative];
        public int NextOf(int v) => next[v];
    }
}
