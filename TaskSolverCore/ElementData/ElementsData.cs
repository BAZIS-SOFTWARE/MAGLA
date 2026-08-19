using MaterialDB.MaterialData;
using MaterialDB.MaterialData.MetallurgicalData;
using Model.Interfaces.MeshObjects;
using Project.Interfaces.Tasks;
using Project.Tasks;
using Project.Tasks.Materials;
using ResultDB;
using System.Collections;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using TaskSolverCore.Matrix;

namespace TaskSolverCore.ElementData
{
    public class ElementsData<T> : IEnumerable<T> where T : ElementItem
    {
        internal Dictionary<int, T> eItems = new Dictionary<int, T>();

        public void Add(T elementItem)
        {
            eItems.Add(elementItem.Number, elementItem);
        }
        /// <summary>
        /// Count
        /// </summary>
        public int Count { get { return eItems.Count; } }


        //List<T> eItems = new List<T>();

        public T this[int ind]
        {
            get
            {
                return eItems[ind];
            }
            set { eItems[ind] = value; }
        }

        public bool ContainsElement(int number)
        {
            return eItems.ContainsKey(number);
        }

        public List<int> GetElementsNumbers()
        {
            return eItems.Keys.ToList();
        }
        public List<int> GetNodesNumbers()
        {
            var set = new HashSet<int>();
            foreach (var item in eItems)
            {
                foreach (var number in item.Value.Element.GetVertexes().Select(x => x.Number))
                    set.Add(number);
            }
            var list = set.ToList();
            list.Sort();
            return list;
        }
        /// <summary>
        /// SetInitialCondition. Заполнение температуры и фаз для 
        /// случая, когда происходит рестарт
        /// </summary>
        /// <param name="result"></param>
        public void SetInitialCondition(Result result)
        {
            //Подумать над правильностью реализации хранения и чтения результатов
            //Сейчас температура и фазовый состав хранятся вместе с теплофизикой
            //и механическими результатами.
            foreach (var item in eItems)
            {
                //обновление температуры
                var row = result.Data.Tables["elements"].Rows.Find(item.Key);

                item.Value.Temp = (float)row["T"];
                //обновление фазового состава
                foreach (var phase in item.Value.PhaseData)
                    phase.Value = (float)row[phase.Name];
            }

        }

        internal HashSet<string> GetPhases()
        {
            var phaseList = new HashSet<string>();

            foreach (var eItem in eItems)
            {
                foreach (var phase in eItem.Value.PhaseData)
                {
                    if (!phaseList.Contains(phase.Name))
                        phaseList.Add(phase.Name);
                }
            }
            return phaseList;
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var eItem in eItems)
            {
                yield return eItem.Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }  
    }
}
