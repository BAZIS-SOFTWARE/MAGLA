namespace CAESolvers
{
    using System;

    /// <summary>
    /// Переупорядочивание симметричной матрицы по алгоритму приближённой
    /// минимальной степени (AMD, Amestoy–Davis–Duff) для снижения заполнения
    /// (fill-in) при прямой факторизации.
    ///
    /// Зачем это нужно. Прямой решатель заменяет матрицу её множителем, и
    /// число ненулевых элементов множителя зависит от порядка исключения
    /// неизвестных на порядки величины. Для трёхмерной задачи МКЭ на сотни
    /// тысяч уравнений факторизация в исходном порядке требует терабайтов
    /// памяти, а после AMD — единиц гигабайт. Перестановка симметрична
    /// (P^T A P), поэтому симметрия и положительная определённость матрицы
    /// сохраняются.
    ///
    /// Как это работает. Алгоритм ведёт «фактор-граф» (quotient graph):
    /// исключённые вершины не удаляются с достройкой клики (это дало бы
    /// квадратичную память), а сливаются в «элементы» — гиперрёбра,
    /// представляющие клику неявно. На каждом шаге выбирается вершина с
    /// минимальной приближённой внешней степенью; приближение (отсюда «A» в
    /// AMD) считается за два прохода по элементам и даёт верхнюю оценку
    /// истинной степени практически той же качества при существенно меньшей
    /// стоимости, чем точная минимальная степень.
    ///
    /// Реализованы все ключевые ускорения оригинального алгоритма, без
    /// которых он непригоден для больших задач:
    /// <list type="bullet">
    /// <item>поглощение элементов (element absorption) и агрессивное
    /// поглощение — умирающие элементы сразу выбрасываются из графа;</item>
    /// <item>массовое исключение (mass elimination) — вершина, единственным
    /// соседом которой остался текущий элемент, исключается вместе с ним без
    /// отдельного шага;</item>
    /// <item>суперпеременные (indistinguishable variables) — вершины с
    /// одинаковыми списками смежности объединяются и далее обрабатываются как
    /// одна, что для МКЭ-матриц с несколькими степенями свободы в узле даёт
    /// кратное ускорение;</item>
    /// <item>уплотнение рабочего массива (garbage collection) — без него
    /// память под списки элементов росла бы как размер множителя.</item>
    /// </list>
    ///
    /// Вершины с аномально большой степенью («плотные» строки — например,
    /// уравнение связи, входящее почти во все остальные) исключаются из
    /// анализа и упорядочиваются последними: они всё равно дают плотный
    /// «хвост» множителя, но не портят оценки степеней для остальных.
    /// </summary>
    public static class ApproximateMinimumDegreeOrdering
    {
        private const int Empty = -1;

        /// <summary>
        /// Множитель порога «плотной» строки: строка считается плотной, если
        /// её степень превышает DenseRowFactor * sqrt(n). Значение 10
        /// соответствует значению по умолчанию в оригинальном AMD.
        /// </summary>
        private const double DenseRowFactor = 10.0;

        /// <summary>
        /// Помечает «мёртвый» указатель. Инволюция: Flip(Flip(x)) == x,
        /// и для любого x >= -1 значение Flip(x) <= -1, поэтому по знаку
        /// указателя всегда видно, жив объект или уже поглощён.
        /// </summary>
        private static int Flip(int i) => -i - 2;

        /// <summary>
        /// Вычисляет перестановку, снижающую заполнение: возвращает массив
        /// permutation, где permutation[k] — исходный номер уравнения,
        /// исключаемого k-м по счёту.
        /// </summary>
        public static int[] Compute(SymmetricPatternGraph graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            int n = graph.Size;
            var permutation = new int[n];
            if (n == 0)
                return permutation;

            var state = new WorkSpace(graph);
            state.Run();
            return state.Permutation;
        }

        /// <summary>
        /// Состояние алгоритма. Вынесено в отдельный класс, чтобы не тащить
        /// полтора десятка рабочих массивов через параметры: основной цикл
        /// AMD плотно работает со всеми ними одновременно.
        /// </summary>
        private sealed class WorkSpace
        {
            private readonly int n;

            // --- Фактор-граф -------------------------------------------------
            private readonly FactorGraph factorGraph;

            // nv[i] > 0  — живая суперпеременная из nv[i] исходных уравнений;
            // nv[i] < 0  — переменная, помеченная как вошедшая в текущий Lme;
            // nv[i] == 0 — мёртвая (поглощённая или плотная).
            private readonly int[] nv;

            // Приближённая внешняя степень (для переменных) либо |Le| (для элементов).
            private readonly int[] degree;

            // Рабочие метки для двухпроходного вычисления степеней.
            // w[e] == 0 означает «элемент мёртв».
            private readonly int[] w;
            private int wflg;
            private readonly int wbig;

            // --- Списки вершин по степеням (bucket sort) ----------------------
            private readonly DegreeBuckets buckets;

            // --- Хеш-таблица для поиска неразличимых переменных ---------------
            private readonly HashBuckets hashBuckets;

            // --- Состав суперпеременных и выход ------------------------------
            private readonly MemberLists members;

            private int permutationCursor;

            // Плотные строки: упорядочиваются в самом конце.
            private readonly int[] denseVariables;
            private int denseCount;

            public int[] Permutation { get; }

            public WorkSpace(SymmetricPatternGraph graph)
            {
                n = graph.Size;

                factorGraph = new FactorGraph(graph);

                nv = new int[n];
                degree = new int[n];
                w = new int[n];

                buckets = new DegreeBuckets(n);

                hashBuckets = new HashBuckets(n);
                members = new MemberLists(n);

                denseVariables = new int[n];
                Permutation = new int[n];

                wbig = int.MaxValue - n - 1;

                for (int i = 0; i < n; i++)
                {
                    nv[i] = 1;
                    w[i] = 1;
                    degree[i] = factorGraph.Len[i];
                }
            }

            public void Run()
            {
                int eliminated = InitialiseDegreeLists();
                wflg = 2;
                buckets.ResetMinDegree();

                int maxElementSize = 0;

                while (eliminated < n)
                {
                    // Метки w[] сравниваются с wflg, а не обнуляются на каждом
                    // шаге. За один шаг wflg и значения меток вырастают не
                    // более чем на 2n, поэтому запас проверяем один раз здесь,
                    // пока в w[] хранится только признак «элемент жив».
                    EnsureMarkCapacity(2 * n + 2);

                    int pivot = SelectPivot();

                    int pivotElen = factorGraph.Elen[pivot];
                    int pivotNv = nv[pivot];
                    eliminated += pivotNv;
                    nv[pivot] = -pivotNv;   // помечаем как исключённую

                    // Строим новый элемент Lme = объединение списков соседних
                    // элементов и соседних переменных, без самой pivot.
                    int elementDegree;
                    int firstEntry;
                    int lastEntry;

                    if (pivotElen == 0)
                        BuildElementFromVariablesOnly(pivot, out elementDegree, out firstEntry, out lastEntry);
                    else
                        BuildElementFromElements(pivot, pivotElen, out elementDegree, out firstEntry, out lastEntry);

                    factorGraph.Pe[pivot] = firstEntry;
                    factorGraph.Len[pivot] = lastEntry - firstEntry + 1;

                    ComputeElementSetDifferences(firstEntry, lastEntry);
                    ComputeApproximateDegrees(pivot, firstEntry, lastEntry, ref elementDegree, ref pivotNv, ref eliminated);

                    // |Le| нового элемента — уже с поправкой на массовое
                    // исключение; именно это значение читает проход 1 на
                    // последующих шагах.
                    degree[pivot] = elementDegree;
                    factorGraph.Elen[pivot] = Flip(pivotNv + elementDegree);
                    maxElementSize = Math.Max(maxElementSize, elementDegree);

                    DetectIndistinguishableVariables();

                    RestoreDegreeLists(pivot, pivotElen, elementDegree, pivotNv, eliminated, firstEntry, lastEntry);

                    EmitEliminatedVariables(pivot);

                    // Сдвиг метки: гарантирует, что ни одно значение w[e],
                    // выставленное на этом шаге, не будет принято за «свежее»
                    // на следующем.
                    wflg += maxElementSize + 1;
                }

                // Плотные строки — в самый конец.
                for (int t = 0; t < denseCount; t++)
                    EmitEliminatedVariables(denseVariables[t]);

                if (permutationCursor != n)
                    throw new InvalidOperationException(
                        $"AMD: построена неполная перестановка ({permutationCursor} из {n} уравнений). " +
                        "Это внутренняя ошибка алгоритма переупорядочивания.");
            }

            /// <summary>
            /// Раскладывает вершины по спискам степеней. Изолированные вершины
            /// (степень 0) исключаются сразу, «плотные» откладываются в конец.
            /// Возвращает число уже исключённых уравнений.
            /// </summary>
            private int InitialiseDegreeLists()
            {
                int dense = (int)(DenseRowFactor * Math.Sqrt(n));
                dense = Math.Max(16, dense);
                dense = Math.Min(n, dense);

                int eliminated = 0;

                for (int i = 0; i < n; i++)
                {
                    int deg = degree[i];

                    if (deg == 0)
                    {
                        // Изолированное уравнение: заполнения не создаёт,
                        // упорядочиваем немедленно.
                        factorGraph.Pe[i] = Empty;
                        w[i] = 0;
                        nv[i] = 0;
                        factorGraph.Elen[i] = Empty;
                        eliminated++;
                        EmitEliminatedVariables(i);
                    }
                    else if (deg > dense)
                    {
                        factorGraph.Pe[i] = Empty;
                        nv[i] = 0;
                        factorGraph.Elen[i] = Empty;
                        eliminated++;
                        denseVariables[denseCount++] = i;
                    }
                    else
                    {
                        InsertIntoDegreeList(i, deg);
                    }
                }

                return eliminated;
            }

            private void InsertIntoDegreeList(int i, int deg) => buckets.Insert(i, deg);

            // deg берётся из текущего degree[i] — тем же значением вершина
            // была вставлена (или перенесена) в корзину последним вызовом
            // InsertIntoDegreeList, а degree[] обновляется только вместе с ним.
            private void RemoveFromDegreeList(int i) => buckets.Remove(i, degree[i]);

            private int SelectPivot() => buckets.PopMinimum(n);

            /// <summary>
            /// Простой случай: у pivot нет соседних элементов, поэтому Lme —
            /// это просто её живые соседи-переменные, и новый элемент можно
            /// собрать на месте, поверх собственного списка pivot.
            /// </summary>
            private void BuildElementFromVariablesOnly(int pivot, out int elementDegree, out int firstEntry, out int lastEntry)
            {
                elementDegree = 0;
                firstEntry = factorGraph.Pe[pivot];
                lastEntry = firstEntry - 1;

                int end = firstEntry + factorGraph.Len[pivot];
                for (int p = firstEntry; p < end; p++)
                {
                    int i = factorGraph.Iw[p];
                    int nvi = nv[i];
                    if (nvi <= 0)
                        continue;

                    elementDegree += nvi;
                    nv[i] = -nvi;               // пометка «входит в Lme»
                    factorGraph.Iw[++lastEntry] = i;
                    RemoveFromDegreeList(i);
                }
            }

            /// <summary>
            /// Общий случай: Lme — объединение множеств всех соседних элементов
            /// и живых соседей-переменных. Собирается в свободном хвосте
            /// рабочего массива; каждый использованный элемент поглощается
            /// новым (его список больше не нужен и его память освобождается
            /// при следующем уплотнении).
            /// </summary>
            private void BuildElementFromElements(int pivot, int pivotElen, out int elementDegree, out int firstEntry, out int lastEntry)
            {
                elementDegree = 0;
                int p = factorGraph.Pe[pivot];
                firstEntry = factorGraph.Pfree;

                // Число собственных соседей-переменных pivot фиксируется один
                // раз: Len[pivot] по ходу цикла переписывается при уплотнении
                // рабочего массива, и выводить это число из него нельзя —
                // иначе часть соседей не попадёт в новый элемент, а pivot
                // останется в их списках уже как элемент с положительным nv и
                // будет принят за живую переменную на следующем шаге.
                int pivotVariableCount = factorGraph.Len[pivot] - pivotElen;

                for (int outer = 1; outer <= pivotElen + 1; outer++)
                {
                    int e;
                    int cursor;
                    int count;
                    bool processingOwnList = outer > pivotElen;

                    if (processingOwnList)
                    {
                        // Последний проход — собственные соседи-переменные pivot.
                        e = pivot;
                        cursor = p;
                        count = pivotVariableCount;
                    }
                    else
                    {
                        e = factorGraph.Iw[p++];
                        cursor = factorGraph.Pe[e];
                        count = factorGraph.Len[e];
                    }

                    for (int inner = 1; inner <= count; inner++)
                    {
                        int i = factorGraph.Iw[cursor++];
                        int nvi = nv[i];
                        if (nvi <= 0)
                            continue;

                        if (factorGraph.NeedsCompaction)
                        {
                            // Сохраняем позиции, по которым продолжим после
                            // уплотнения: оно двигает все списки влево.
                            if (!processingOwnList)
                            {
                                factorGraph.Pe[pivot] = p;
                                factorGraph.Len[pivot] = (pivotElen - outer) + pivotVariableCount;
                                if (factorGraph.Len[pivot] == 0)
                                    factorGraph.Pe[pivot] = Empty;
                            }

                            factorGraph.Pe[e] = cursor;
                            factorGraph.Len[e] = count - inner;
                            if (factorGraph.Len[e] == 0)
                                factorGraph.Pe[e] = Empty;

                            factorGraph.Compact(ref firstEntry);

                            cursor = factorGraph.Pe[e];
                            p = processingOwnList ? cursor : factorGraph.Pe[pivot];
                        }

                        elementDegree += nvi;
                        nv[i] = -nvi;
                        factorGraph.Iw[factorGraph.Pfree++] = i;
                        RemoveFromDegreeList(i);
                    }

                    if (e != pivot)
                    {
                        // Поглощение элемента e новым элементом pivot.
                        factorGraph.Pe[e] = Flip(pivot);
                        w[e] = 0;
                    }
                }

                lastEntry = factorGraph.Pfree - 1;
            }

            /// <summary>
            /// Проход 1 вычисления приближённых степеней: для каждого элемента
            /// e, смежного с вершинами нового элемента, считает |Le \ Lme| —
            /// сколько вершин e осталось вне Lme. Значение накапливается в
            /// метке w[e] относительно текущего wflg, что позволяет обойтись
            /// без отдельной очистки массива на каждом шаге.
            /// </summary>
            private void ComputeElementSetDifferences(int firstEntry, int lastEntry)
            {
                for (int position = firstEntry; position <= lastEntry; position++)
                {
                    int i = factorGraph.Iw[position];
                    int elementCount = factorGraph.Elen[i];
                    if (elementCount <= 0)
                        continue;

                    int nvi = -nv[i];
                    int wnvi = wflg - nvi;

                    int end = factorGraph.Pe[i] + elementCount;
                    for (int p = factorGraph.Pe[i]; p < end; p++)
                    {
                        int e = factorGraph.Iw[p];
                        int we = w[e];

                        if (we >= wflg)
                            we -= nvi;
                        else if (we != 0)
                            we = degree[e] + wnvi;

                        w[e] = we;
                    }
                }
            }

            /// <summary>
            /// Проход 2: собственно приближённая внешняя степень каждой
            /// вершины Lme, попутно — агрессивное поглощение элементов,
            /// массовое исключение вершин и подготовка хеш-таблицы для поиска
            /// неразличимых переменных.
            /// </summary>
            private void ComputeApproximateDegrees(
                int pivot, int firstEntry, int lastEntry,
                ref int elementDegree, ref int pivotNv, ref int eliminated)
            {
                hashBuckets.BeginRound();

                for (int position = firstEntry; position <= lastEntry; position++)
                {
                    int i = factorGraph.Iw[position];
                    int listStart = factorGraph.Pe[i];
                    int elementEnd = listStart + factorGraph.Elen[i];
                    int write = listStart;

                    long hash = 0;
                    int deg = 0;

                    // Соседние элементы: живые оставляем, пустые поглощаем.
                    for (int p = listStart; p < elementEnd; p++)
                    {
                        int e = factorGraph.Iw[p];
                        if (w[e] == 0)
                            continue;

                        int external = w[e] - wflg;
                        if (external > 0)
                        {
                            deg += external;
                            factorGraph.Iw[write++] = e;
                            hash += e;
                        }
                        else if (external == 0)
                        {
                            // Агрессивное поглощение: Le целиком содержится
                            // в Lme, отдельно хранить его больше не нужно.
                            factorGraph.Pe[e] = Flip(pivot);
                            w[e] = 0;
                        }
                    }

                    // +1 — место под сам pivot, который станет первым элементом списка.
                    factorGraph.Elen[i] = write - listStart + 1;

                    int variableStart = write;
                    int listEnd = listStart + factorGraph.Len[i];

                    // Соседние переменные вне Lme.
                    for (int p = elementEnd; p < listEnd; p++)
                    {
                        int j = factorGraph.Iw[p];
                        int nvj = nv[j];
                        if (nvj <= 0)
                            continue;

                        deg += nvj;
                        factorGraph.Iw[write++] = j;
                        hash += j;
                    }

                    if (factorGraph.Elen[i] == 1 && variableStart == write)
                    {
                        // Массовое исключение: у i не осталось соседей, кроме
                        // pivot, — исключаем её тем же шагом.
                        factorGraph.Pe[i] = Flip(pivot);
                        int nvi = -nv[i];
                        elementDegree -= nvi;
                        pivotNv += nvi;
                        eliminated += nvi;
                        nv[i] = 0;
                        factorGraph.Elen[i] = Empty;

                        members.Append(pivot, i);
                    }
                    else
                    {
                        degree[i] = Math.Min(degree[i], deg);

                        // Ставим pivot первым в списке i: сдвигаем первую
                        // переменную в конец, первый элемент — на её место.
                        factorGraph.Iw[write] = factorGraph.Iw[variableStart];
                        factorGraph.Iw[variableStart] = factorGraph.Iw[listStart];
                        factorGraph.Iw[listStart] = pivot;
                        factorGraph.Len[i] = write - listStart + 1;

                        hashBuckets.Add(hash, i);
                    }
                }
            }

            /// <summary>
            /// Поиск и слияние неразличимых переменных: если у двух вершин
            /// нового элемента совпадают списки смежности (а значит, и все
            /// будущие степени и заполнение), их можно объединить в одну
            /// суперпеременную. Сравниваются только вершины с одинаковым
            /// хешем, поэтому проверка обходится дешево.
            /// </summary>
            private void DetectIndistinguishableVariables()
            {
                for (int b = 0; b < hashBuckets.UsedCount; b++)
                {
                    int bucket = hashBuckets.UsedBucketAt(b);

                    for (int i = hashBuckets.First(bucket); i != Empty; i = hashBuckets.NextOf(i))
                    {
                        if (nv[i] >= 0)
                            continue;   // уже поглощена другой суперпеременной

                        int length = factorGraph.Len[i];
                        int elementCount = factorGraph.Elen[i];

                        // Первый элемент списка — pivot, общий для всех
                        // вершин Lme, его можно не сравнивать.
                        int start = factorGraph.Pe[i] + 1;
                        int end = factorGraph.Pe[i] + length;
                        for (int p = start; p < end; p++)
                            w[factorGraph.Iw[p]] = wflg;

                        int j = hashBuckets.NextOf(i);
                        while (j != Empty)
                        {
                            bool identical = nv[j] < 0 && factorGraph.Len[j] == length && factorGraph.Elen[j] == elementCount;

                            if (identical)
                            {
                                int jStart = factorGraph.Pe[j] + 1;
                                int jEnd = factorGraph.Pe[j] + length;
                                for (int p = jStart; p < jEnd; p++)
                                {
                                    if (w[factorGraph.Iw[p]] != wflg)
                                    {
                                        identical = false;
                                        break;
                                    }
                                }
                            }

                            int next = hashBuckets.NextOf(j);

                            if (identical)
                            {
                                factorGraph.Pe[j] = Flip(i);
                                nv[i] += nv[j];     // оба отрицательные — счётчик копится
                                nv[j] = 0;
                                factorGraph.Elen[j] = Empty;

                                members.Append(i, j);
                                hashBuckets.Unlink(j);   // O(1): двусвязная цепочка, previous не нужен
                            }

                            j = next;
                        }

                        wflg++;
                    }

                    hashBuckets.ClearBucket(bucket);
                }
            }

            /// <summary>
            /// Возвращает выжившие вершины нового элемента в списки степеней с
            /// обновлённой внешней степенью и вычищает из списка элемента
            /// поглощённые вершины.
            /// </summary>
            private void RestoreDegreeLists(
                int pivot, int pivotElen, int elementDegree, int pivotNv, int eliminated,
                int firstEntry, int lastEntry)
            {
                int write = firstEntry;
                int remaining = n - eliminated;

                for (int position = firstEntry; position <= lastEntry; position++)
                {
                    int i = factorGraph.Iw[position];
                    int nvi = -nv[i];
                    if (nvi <= 0)
                        continue;

                    nv[i] = nvi;

                    // Внешняя степень = (степень без вклада Lme) + |Lme \ i|,
                    // но не больше числа оставшихся неизвестных.
                    int deg = degree[i] + elementDegree - nvi;
                    deg = Math.Min(deg, remaining - nvi);
                    if (deg < 0)
                        deg = 0;

                    degree[i] = deg;
                    InsertIntoDegreeList(i, deg);

                    factorGraph.Iw[write++] = i;
                }

                nv[pivot] = pivotNv;
                factorGraph.Len[pivot] = write - firstEntry;

                if (factorGraph.Len[pivot] == 0)
                {
                    // Элемент оказался пустым — он мёртв сразу после рождения.
                    factorGraph.Pe[pivot] = Empty;
                    w[pivot] = 0;
                }

                if (pivotElen != 0)
                    factorGraph.Pfree = write;
            }

            /// <summary>
            /// Гарантирует, что счётчик метки wflg можно увеличить на
            /// требуемую величину без переполнения int; при необходимости
            /// сбрасывает метки живых элементов.
            /// </summary>
            private void EnsureMarkCapacity(int required)
            {
                if (wflg + required < wbig)
                    return;

                for (int x = 0; x < n; x++)
                {
                    if (w[x] != 0)
                        w[x] = 1;
                }

                wflg = 2;
            }

            /// <summary>
            /// Выводит все исходные уравнения, представленные суперпеременной,
            /// в перестановку — подряд, что и требуется: неразличимые
            /// неизвестные исключаются одним блоком.
            /// </summary>
            private void EmitEliminatedVariables(int representative)
            {
                for (int v = members.First(representative); v != Empty; v = members.NextOf(v))
                    Permutation[permutationCursor++] = v;
            }
        }
    }
}
