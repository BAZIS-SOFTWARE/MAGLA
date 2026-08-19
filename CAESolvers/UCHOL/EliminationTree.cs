namespace CAESolvers
{
    using System;

    /// <summary>
    /// Дерево исключений (elimination tree) симметричной матрицы и связанные с
    /// ним символьные величины. Это центральная структура прямых решателей:
    /// родитель столбца j — это номер первой строки ниже диагонали в столбце j
    /// множителя, и всё остальное следует из этого.
    ///
    /// Дерево даёт три вещи, без которых суперузловой мультифронтальный
    /// решатель не построить:
    /// <list type="bullet">
    /// <item>порядок обработки: столбец нельзя факторизовать, пока не
    /// обработаны его потомки, — а независимые поддеревья можно обрабатывать
    /// одновременно, и именно это даёт распараллеливание по ядрам;</item>
    /// <item>длины столбцов множителя (<see cref="ColumnCounts"/>) до самой
    /// факторизации — по ним заранее известны и объём памяти, и границы
    /// суперузлов;</item>
    /// <item>обратный обход (<see cref="Postorder"/>), после которого столбцы
    /// одного суперузла идут подряд, а поддеревья занимают непрерывные
    /// диапазоны номеров.</item>
    /// </list>
    /// Все методы предполагают, что матрица уже переставлена так, что
    /// parent[j] &gt; j (это верно для любой матрицы в её собственной
    /// нумерации).
    /// </summary>
    public static class EliminationTree
    {
        private const int Empty = -1;

        /// <summary>
        /// Строит дерево исключений за почти линейное время. Используется
        /// сжатие путей через массив ancestor: каждая пройденная вершина сразу
        /// переподвешивается к текущему столбцу, поэтому повторные проходы по
        /// длинным цепочкам не повторяются.
        /// </summary>
        /// <returns>parent[j] — родитель столбца j, либо -1 для корня.</returns>
        public static int[] Build(int size, int[] pointers, int[] rows)
        {
            var parent = new int[size];
            var ancestor = new int[size];

            for (int k = 0; k < size; k++)
            {
                parent[k] = Empty;
                ancestor[k] = Empty;

                int end = pointers[k + 1];
                for (int p = pointers[k]; p < end; p++)
                {
                    int i = rows[p];
                    if (i >= k)
                        break;      // столбец отсортирован: дальше только i > k

                    // Поднимаемся от i к корню его текущего поддерева,
                    // подвешивая всё пройденное к k.
                    while (i != Empty && i < k)
                    {
                        int nextAncestor = ancestor[i];
                        ancestor[i] = k;
                        if (nextAncestor == Empty)
                            parent[i] = k;

                        i = nextAncestor;
                    }
                }
            }

            return parent;
        }

        /// <summary>
        /// Обратный обход дерева (postorder): каждая вершина идёт после всех
        /// своих потомков. Реализован итеративно на явном стеке — глубина
        /// дерева исключений на «вытянутых» задачах достигает n, и рекурсия
        /// здесь переполнила бы стек.
        /// </summary>
        /// <returns>postorder[k] — вершина, стоящая в обходе на k-м месте.</returns>
        public static int[] Postorder(int size, int[] parent)
        {
            var head = new int[size];
            var next = new int[size];
            var stack = new int[size];
            var postorder = new int[size];

            Array.Fill(head, Empty);

            // Списки детей. Обход в обратном порядке даёт детей в порядке
            // возрастания номера — обход получается детерминированным.
            for (int j = size - 1; j >= 0; j--)
            {
                if (parent[j] == Empty)
                    continue;

                next[j] = head[parent[j]];
                head[parent[j]] = j;
            }

            int written = 0;
            for (int j = 0; j < size; j++)
            {
                if (parent[j] != Empty)
                    continue;

                written = TraverseSubtree(j, written, head, next, postorder, stack);
            }

            return postorder;
        }

        /// <summary>
        /// Обход одного поддерева. Список детей по ходу «съедается» (head
        /// сдвигается), что заменяет отдельный массив состояния итератора.
        /// </summary>
        private static int TraverseSubtree(int root, int written, int[] head, int[] next, int[] postorder, int[] stack)
        {
            int top = 0;
            stack[0] = root;

            while (top >= 0)
            {
                int node = stack[top];
                int child = head[node];

                if (child == Empty)
                {
                    top--;
                    postorder[written++] = node;
                }
                else
                {
                    head[node] = next[child];
                    stack[++top] = child;
                }
            }

            return written;
        }

        /// <summary>
        /// Число ненулевых элементов в каждом столбце множителя L (включая
        /// диагональ), вычисленное по алгоритму Гилберта–Нг–Пейтона за почти
        /// линейное время — без построения самой структуры множителя, которая
        /// для больших задач в память бы не поместилась.
        ///
        /// Идея: вклад строки i в столбец j возникает только если j — «лист»
        /// поддерева строки i; такие листья распознаются за амортизированное
        /// O(1) через сжатие путей (<see cref="FindLeaf"/>), а итоговые длины
        /// получаются суммированием поправок вверх по дереву.
        /// </summary>
        public static int[] ColumnCounts(int size, int[] pointers, int[] rows, int[] parent, int[] postorder)
        {
            var delta = new int[size];
            var ancestor = new int[size];
            var maxFirst = new int[size];
            var previousLeaf = new int[size];
            var first = new int[size];

            Array.Fill(maxFirst, Empty);
            Array.Fill(previousLeaf, Empty);
            Array.Fill(first, Empty);

            // first[j] — позиция в обходе самого раннего потомка j.
            for (int k = 0; k < size; k++)
            {
                int j = postorder[k];
                delta[j] = first[j] == Empty ? 1 : 0;

                for (; j != Empty && first[j] == Empty; j = parent[j])
                    first[j] = k;
            }

            for (int i = 0; i < size; i++)
                ancestor[i] = i;

            for (int k = 0; k < size; k++)
            {
                int j = postorder[k];
                if (parent[j] != Empty)
                    delta[parent[j]]--;

                int end = pointers[j + 1];
                for (int p = pointers[j]; p < end; p++)
                {
                    int i = rows[p];
                    int leastCommon = FindLeaf(i, j, first, maxFirst, previousLeaf, ancestor, out int leafKind);

                    if (leafKind >= 1)
                        delta[j]++;
                    if (leafKind == 2)
                        delta[leastCommon]--;
                }

                if (parent[j] != Empty)
                    ancestor[j] = parent[j];
            }

            // Суммирование поправок вверх по дереву. parent[j] > j, поэтому
            // одного прохода по возрастанию достаточно.
            for (int j = 0; j < size; j++)
            {
                if (parent[j] != Empty)
                    delta[parent[j]] += delta[j];
            }

            return delta;
        }

        /// <summary>
        /// Определяет, является ли j листом поддерева строки i, и если это уже
        /// не первый лист — возвращает наименьшего общего предка с предыдущим
        /// листом, вклад которого нужно вычесть, чтобы не посчитать общую часть
        /// пути дважды.
        /// </summary>
        /// <param name="leafKind">0 — не лист, 1 — первый лист, 2 — очередной лист.</param>
        private static int FindLeaf(
            int i, int j, int[] first, int[] maxFirst, int[] previousLeaf, int[] ancestor, out int leafKind)
        {
            leafKind = 0;

            if (i <= j || first[j] <= maxFirst[i])
                return Empty;

            maxFirst[i] = first[j];
            int jPrevious = previousLeaf[i];
            previousLeaf[i] = j;

            if (jPrevious == Empty)
            {
                leafKind = 1;
                return i;
            }

            leafKind = 2;

            int root = jPrevious;
            while (root != ancestor[root])
                root = ancestor[root];

            for (int s = jPrevious; s != root;)
            {
                int nextNode = ancestor[s];
                ancestor[s] = root;
                s = nextNode;
            }

            return root;
        }
    }
}
