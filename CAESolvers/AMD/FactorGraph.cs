namespace CAESolvers
{
    using System;

    /// <summary>
    /// Фактор-граф (quotient graph): списки смежности живут в одном общем
    /// массиве Iw. Для живой переменной i её список — это
    /// Iw[Pe[i] .. Pe[i]+Len[i]-1], причём сначала идут Elen[i] соседних
    /// элементов, затем переменные. Для элемента e список — это множество
    /// Le его вершин, а Elen[e] &lt; -1 (значение Flip(...)) отличает
    /// элемент от переменной.
    ///
    /// В отличие от DegreeBuckets/HashBuckets/MemberLists этот класс не
    /// скрывает Iw/Pe/Len/Elen за узким интерфейсом из 2-3 операций: они
    /// читаются и переписываются в тысячах комбинаций внутри самого
    /// алгоритма (вперемешку с nv/degree/w, которые ему не принадлежат),
    /// и обёртка каждого обращения в метод превратила бы этот класс в
    /// протекающую абстракцию без реальной инкапсуляции, только с
    /// лишним уровнем вызовов в самых горячих циклах AMD. Поэтому массивы
    /// остаются публичными для прямого поэлементного доступа — класс
    /// берёт на себя только то, что действительно самостоятельно: их
    /// совместный жизненный цикл (создание, рост) и уплотнение
    /// (<see cref="Compact"/>), которое само по себе не трогает
    /// nv/degree/w и всегда было отдельным методом.
    ///
    /// В отличие от DegreeBuckets/HashBuckets/MemberLists у этого класса нет
    /// собственного поля-маркера «пусто»: он сам никогда не сравнивает и не
    /// присваивает значение-«пусто» — это делает только вызывающий
    /// (ApproximateMinimumDegreeOrdering.WorkSpace), когда пишет Empty в
    /// Pe[i]/Elen[i] через публичные свойства. Для самого FactorGraph это
    /// просто одно из значений int, ничем не отличающееся от любого другого.
    /// </summary>
    internal sealed class FactorGraph
    {
        private int[] iw;
        private int iwLength;
        private int pfree;
        private readonly int n;

        // Скратч для уплотнения — используется только здесь.
        private int[]? compactOrder;
        private int[]? compactKeys;

        public int[] Pe { get; }
        public int[] Len { get; }
        public int[] Elen { get; }

        public FactorGraph(SymmetricPatternGraph graph)
        {
            n = graph.Size;
            int edgeEntries = graph.Neighbors.Length;

            // Запас нужен под списки новых элементов, которые дописываются
            // в конец массива. Уплотнение освобождает место от поглощённых
            // элементов, поэтому большой запас не обязателен, но снижает
            // число уплотнений.
            iwLength = edgeEntries + edgeEntries / 5 + n + 16;
            iw = new int[iwLength];
            Array.Copy(graph.Neighbors, iw, edgeEntries);
            pfree = edgeEntries;

            Pe = new int[n];
            Len = new int[n];
            Elen = new int[n];

            for (int i = 0; i < n; i++)
            {
                Pe[i] = graph.Pointers[i];
                Len[i] = graph.GetDegree(i);
                Elen[i] = 0;
            }
        }

        /// <summary>
        /// Текущий буфер списков смежности. Свойство, а не поле, потому
        /// что Compact может пересоздать буфер (Array.Resize) — каждое
        /// обращение Iw[...] должно видеть актуальную ссылку, а не ту,
        /// что была захвачена до расширения.
        /// </summary>
        public int[] Iw => iw;

        public int Pfree { get => pfree; set => pfree = value; }

        /// <summary>Свободного места в Iw для новых записей уже не осталось.</summary>
        public bool NeedsCompaction => pfree >= iwLength;

        /// <summary>
        /// Сдвигает все живые списки смежности к началу рабочего массива,
        /// выбрасывая память поглощённых элементов. Живые списки не
        /// пересекаются и упорядочены по возрастанию смещения, поэтому
        /// копирование влево в этом же порядке безопасно и не требует
        /// второго массива. Если после уплотнения места всё равно нет,
        /// массив расширяется.
        /// </summary>
        public void Compact(ref int firstEntry)
        {
            compactOrder ??= new int[n];
            compactKeys ??= new int[n];

            int count = 0;
            for (int j = 0; j < n; j++)
            {
                if (Pe[j] >= 0 && Len[j] > 0)
                {
                    compactKeys[count] = Pe[j];
                    compactOrder[count] = j;
                    count++;
                }
            }

            Array.Sort(compactKeys, compactOrder, 0, count);

            int destination = 0;
            for (int t = 0; t < count; t++)
            {
                int j = compactOrder[t];
                int source = Pe[j];
                int length = Len[j];

                if (destination != source)
                    Array.Copy(iw, source, iw, destination, length);

                Pe[j] = destination;
                destination += length;
            }

            // Переносим частично построенный элемент, который лежит
            // за всеми живыми списками.
            int partialLength = pfree - firstEntry;
            if (partialLength > 0 && destination != firstEntry)
                Array.Copy(iw, firstEntry, iw, destination, partialLength);

            firstEntry = destination;
            pfree = destination + partialLength;

            // Гарантируем, что после уплотнения есть куда писать дальше.
            int required = pfree + n + 16;
            if (required > iwLength)
            {
                iwLength = Math.Max(required, iwLength + iwLength / 2);
                Array.Resize(ref iw, iwLength);
            }
        }
    }
}
