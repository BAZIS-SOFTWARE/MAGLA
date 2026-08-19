using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskSolverCore.Matrix
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Разреженная матрица в формате CSR с доступом по индексам за O(1)
    /// </summary>
    public class SparseMatrixCSR
    {
        private double[] values;      // Значения ненулевых элементов
        private int[] colIndices;     // Индексы столбцов
        private int[] rowPointers;    // Указатели на начало строк

        private int rows;
        private int cols;
        private int nonZeroCount;

        // Словарь для быстрого доступа по глобальным индексам (row, col)
        private Dictionary<(int row, int col), int> positionMap;

        /// <summary>
        /// Конструктор матрицы
        /// </summary>
        /// <param name="rows">Количество строк</param>
        /// <param name="cols">Количество столбцов</param>
        public SparseMatrixCSR(int rows, int cols)
        {
            this.rows = rows;
            this.cols = cols;
            this.nonZeroCount = 0;
            this.positionMap = new Dictionary<(int, int), int>();

            // Инициализируем rowPointers размером rows+1 (пока все нули)
            rowPointers = new int[rows + 1];
            values = Array.Empty<double>();
            colIndices = Array.Empty<int>();
        }

        /// <summary>
        /// Конструктор из списка элементов (координатный формат)
        /// </summary>
        public SparseMatrixCSR(int rows, int cols, List<(int row, int col, double value)> elements)
            : this(rows, cols)
        {
            BuildFromCoordinateFormat(elements);
        }

        /// <summary>
        /// Построение CSR из координатного формата
        /// </summary>
        private void BuildFromCoordinateFormat(List<(int row, int col, double value)> elements)
        {
            // Группируем элементы по строкам и сортируем внутри строк по столбцам
            var grouped = elements
                .Where(e => e.value != 0)  // Игнорируем нулевые элементы
                .GroupBy(e => e.row)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Row = g.Key,
                    Items = g.OrderBy(e => e.col).ToList()
                })
                .ToList();

            // Подсчитываем количество ненулевых элементов
            nonZeroCount = elements.Count(e => e.value != 0);

            // Инициализируем массивы
            values = new double[nonZeroCount];
            colIndices = new int[nonZeroCount];
            rowPointers = new int[rows + 1];
            positionMap = new Dictionary<(int, int), int>();

            int currentIndex = 0;
            int currentRow = 0;

            foreach (var group in grouped)
            {
                // Заполняем указатели для пустых строк
                while (currentRow < group.Row)
                {
                    rowPointers[currentRow + 1] = currentIndex;
                    currentRow++;
                }

                // Устанавливаем указатель начала текущей строки
                rowPointers[currentRow] = currentIndex;

                // Заполняем элементы строки
                foreach (var item in group.Items)
                {
                    values[currentIndex] = item.value;
                    colIndices[currentIndex] = item.col;
                    positionMap[(item.row, item.col)] = currentIndex;
                    currentIndex++;
                }

                currentRow++;
            }

            // Заполняем указатели для оставшихся пустых строк
            while (currentRow <= rows)
            {
                rowPointers[currentRow] = currentIndex;
                currentRow++;
            }
        }

        /// <summary>
        /// Доступ к элементу матрицы по глобальным индексам (O(1))
        /// </summary>
        public double this[int row, int col]
        {
            get
            {
                if (row < 0 || row >= rows || col < 0 || col >= cols)
                    throw new IndexOutOfRangeException($"Индексы вне диапазона: ({row}, {col})");

                // Быстрый поиск через словарь O(1)
                if (positionMap.TryGetValue((row, col), out int index))
                    return values[index];

                return 0.0; // Нулевой элемент
            }
            set
            {
                if (row < 0 || row >= rows || col < 0 || col >= cols)
                    throw new IndexOutOfRangeException($"Индексы вне диапазона: ({row}, {col})");

                if (Math.Abs(value) < 1e-15) // Если значение близко к нулю
                {
                    // Удаляем элемент, если он существует
                    if (positionMap.TryGetValue((row, col), out int index))
                    {
                        RemoveElementAtIndex(index);
                        positionMap.Remove((row, col));
                        nonZeroCount--;
                    }
                    return;
                }

                // Если элемент существует - обновляем значение
                if (positionMap.TryGetValue((row, col), out int existingIndex))
                {
                    values[existingIndex] = value;
                }
                else
                {
                    // Добавляем новый элемент (неэффективно, но для O(1) доступа необходимо)
                    // В реальном МКЭ матрица обычно строится один раз
                    AddElement(row, col, value);
                }
            }
        }

        /// <summary>
        /// Добавление нового элемента (вспомогательный метод)
        /// </summary>
        private void AddElement(int row, int col, double value)
        {
            // Создаем новые массивы с увеличенным размером
            int newSize = nonZeroCount + 1;
            var newValues = new double[newSize];
            var newColIndices = new int[newSize];

            // Копируем существующие элементы
            Array.Copy(values, newValues, nonZeroCount);
            Array.Copy(colIndices, newColIndices, nonZeroCount);

            // Добавляем новый элемент в конец
            newValues[nonZeroCount] = value;
            newColIndices[nonZeroCount] = col;

            values = newValues;
            colIndices = newColIndices;

            // Обновляем positionMap для нового элемента
            positionMap[(row, col)] = nonZeroCount;

            // Обновляем rowPointers для строк после row
            for (int i = row + 1; i <= rows; i++)
            {
                rowPointers[i]++;
            }

            nonZeroCount++;
        }

        /// <summary>
        /// Удаление элемента по индексу
        /// </summary>
        private void RemoveElementAtIndex(int index)
        {
            // Создаем новые массивы с уменьшенным размером
            int newSize = nonZeroCount - 1;
            var newValues = new double[newSize];
            var newColIndices = new int[newSize];

            // Копируем элементы до index
            Array.Copy(values, 0, newValues, 0, index);
            Array.Copy(colIndices, 0, newColIndices, 0, index);

            // Копируем элементы после index
            Array.Copy(values, index + 1, newValues, index, newSize - index);
            Array.Copy(colIndices, index + 1, newColIndices, index, newSize - index);

            values = newValues;
            colIndices = newColIndices;

            // Обновляем positionMap для элементов после index
            var keysToUpdate = positionMap.Where(kv => kv.Value > index).ToList();
            foreach (var kv in keysToUpdate)
            {
                positionMap[kv.Key] = kv.Value - 1;
            }
        }

        /// <summary>
        /// Умножение матрицы на вектор
        /// </summary>
        public double[] Multiply(double[] vector)
        {
            if (vector.Length != cols)
                throw new ArgumentException($"Размер вектора {vector.Length} не соответствует числу столбцов {cols}");

            double[] result = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                double sum = 0.0;
                int start = rowPointers[i];
                int end = rowPointers[i + 1];

                for (int j = start; j < end; j++)
                {
                    sum += values[j] * vector[colIndices[j]];
                }

                result[i] = sum;
            }

            return result;
        }

        /// <summary>
        /// Получение строки матрицы в виде словаря (индекс столбца -> значение)
        /// </summary>
        public Dictionary<int, double> GetRow(int row)
        {
            if (row < 0 || row >= rows)
                throw new IndexOutOfRangeException($"Индекс строки {row} вне диапазона");

            var rowElements = new Dictionary<int, double>();
            int start = rowPointers[row];
            int end = rowPointers[row + 1];

            for (int i = start; i < end; i++)
            {
                rowElements[colIndices[i]] = values[i];
            }

            return rowElements;
        }

        /// <summary>
        /// Получение количества ненулевых элементов
        /// </summary>
        public int NonZeroCount => nonZeroCount;

        /// <summary>
        /// Получение размеров матрицы
        /// </summary>
        public (int rows, int cols) Size => (rows, cols);

        /// <summary>
        /// Проверка, является ли элемент нулевым
        /// </summary>
        public bool IsZero(int row, int col)
        {
            return !positionMap.ContainsKey((row, col));
        }

        /// <summary>
        /// Вывод матрицы в консоль (для отладки)
        /// </summary>
        public void Print()
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double value = this[i, j];
                    Console.Write($"{value,8:F3} ");
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Вывод внутреннего представления CSR
        /// </summary>
        public void PrintCSR()
        {
            Console.WriteLine("CSR Representation:");
            Console.WriteLine($"Rows: {rows}, Cols: {cols}, NonZero: {nonZeroCount}");
            Console.WriteLine($"RowPointers: [{string.Join(", ", rowPointers)}]");
            Console.WriteLine($"ColIndices:  [{string.Join(", ", colIndices)}]");
            Console.WriteLine($"Values:      [{string.Join(", ", values.Select(v => v.ToString("F3")))}]");
        }
    }
}
