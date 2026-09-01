namespace CAESolvers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Раскладка множителя по памяти: где именно лежит блок каждого суперузла.
    ///
    /// Блоки не выделяются по отдельности, а нарезаются из небольшого числа
    /// крупных «плит». Причина не в экономии на вызовах выделения памяти, а в
    /// поведении кучи больших объектов в .NET: она не уплотняется, и десятки
    /// тысяч разнокалиберных долгоживущих массивов оставляют в ней дыры, из-за
    /// которых процесс занимает существенно больше памяти, чем весит сам
    /// множитель. На задаче в сотни тысяч уравнений память — как раз тот
    /// ресурс, который определяет применимость прямого решателя, поэтому
    /// предсказуемость здесь важнее простоты.
    ///
    /// Второе следствие: вся память под множитель запрашивается до начала
    /// численной фазы. Если её не хватает, расчёт прекращается сразу, а не
    /// через минуты счёта на середине факторизации.
    ///
    /// Раскладка определяется только структурой разреженности, поэтому
    /// вычисляется в символьной фазе и переиспользуется при повторных
    /// факторизациях.
    /// </summary>
    internal sealed class FactorStorageLayout
    {
        /// <summary>
        /// Целевой размер плиты в элементах (64 МБ). Достаточно крупная, чтобы
        /// плит было немного, и достаточно небольшая, чтобы неиспользованный
        /// хвост последней плиты не был заметен.
        /// </summary>
        private const int TargetSlabLength = 1 << 23;

        private FactorStorageLayout(int[] slabIndex, int[] slabOffset, int[] slabLengths, int[] blockLengths)
        {
            SlabIndex = slabIndex;
            SlabOffset = slabOffset;
            SlabLengths = slabLengths;
            BlockLengths = blockLengths;
        }

        /// <summary>Номер плиты, в которой лежит блок суперузла.</summary>
        public int[] SlabIndex { get; }

        /// <summary>Смещение блока суперузла внутри своей плиты.</summary>
        public int[] SlabOffset { get; }

        /// <summary>Длины плит в элементах.</summary>
        public int[] SlabLengths { get; }

        /// <summary>Длины блоков суперузлов в элементах.</summary>
        public int[] BlockLengths { get; }

        public static FactorStorageLayout Build(SupernodalStructure supernodes)
        {
            int count = supernodes.Count;
            var slabIndex = new int[count];
            var slabOffset = new int[count];
            var blockLengths = new int[count];
            var slabLengths = new List<int>();

            int currentSlab = 0;
            int currentUsed = 0;
            int currentCapacity = 0;

            for (int s = 0; s < count; s++)
            {
                long front = supernodes.FrontSize(s);
                long width = supernodes.Width(s);
                long length = front * width - width * (width - 1) / 2;

                if (length > int.MaxValue)
                    throw new InvalidOperationException(
                        $"Factor block of supernode {s} cannot be represented by a single array " +
                        $"({length} elements). The problem is too dense for the direct solver.");

                blockLengths[s] = (int)length;

                if (currentUsed + length > currentCapacity)
                {
                    // Начинаем новую плиту. Блок может быть крупнее целевого
                    // размера плиты — тогда плита делается под него.
                    if (currentCapacity > 0)
                    {
                        slabLengths.Add(currentUsed);
                        currentSlab++;
                    }

                    currentCapacity = (int)Math.Max(TargetSlabLength, length);
                    currentUsed = 0;
                }

                slabIndex[s] = currentSlab;
                slabOffset[s] = currentUsed;
                currentUsed += (int)length;
            }

            if (currentCapacity > 0)
                slabLengths.Add(currentUsed);

            return new FactorStorageLayout(slabIndex, slabOffset, slabLengths.ToArray(), blockLengths);
        }

        /// <summary>
        /// Выделяет плиты. Содержимое не инициализируется: каждый элемент
        /// множителя будет записан численной фазой ровно один раз.
        /// </summary>
        public double[][] Allocate()
        {
            var slabs = new double[SlabLengths.Length][];
            for (int i = 0; i < SlabLengths.Length; i++)
                slabs[i] = GC.AllocateUninitializedArray<double>(SlabLengths[i]);

            return slabs;
        }
    }
}
