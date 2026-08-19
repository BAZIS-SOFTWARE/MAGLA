using Model.Interfaces.MeshObjects;
using TaskSolverCore.Utilities;

namespace TaskSolverCore
{
    public class NodesData
    {
        //Dictionary<int,int> elements;
        Dictionary<int,int> nodes;
        //List<int>[] elsNdsInds;
        //public int Degrees { get; set; } = 1;

        //public MatrixIndex MatrixIndex { get; }



        public IEnumerable<int> GetNodesNumbs
        {
            get
            {
                foreach (var node in nodes.Keys)
                    yield return node;
            }
        }
        /// <summary>
        /// замена индексов
        /// </summary>
        /// <param name="order"></param>
        public void SetNewOrder(Dictionary<int, int> order)
        {
            foreach (var node in nodes)
            {
                var change = order[node.Value];
                nodes[node.Key] = change;
            }
            //for (int i = 0; i < order.Count; i++)
            //    nodes[i] = order[i];
        }

        public bool ContainsNode(int number)
        {
            return nodes.ContainsKey(number);
        }

        public int IndexOfNode(int number)
        {
            return nodes[number];
        }
        /// <summary>
        /// Count nodes
        /// </summary>
        public int Count { get { return nodes.Count; } }

        public NodesData(List<int> numbNodes)
        {
            //TO DO

            var ndCounter = 0;
            nodes = numbNodes.ToDictionary(x => x, y => ndCounter++);
        }

        public int GetBandWidth(IEnumerable<IElement> elements)
        {
            var incMatr = GetLocalNodesInc(elements);
            var iniGraph = new Graph(incMatr.Length);

            return iniGraph.GetBandWidth();
        }

        public int GetGlobalBand(List<int>[] incMatr)
        {
            var gBand = 0;

            var counter = 0;
            foreach (var item in incMatr)
            {
                var max = item.Max();

                if (max - counter > gBand)
                    gBand = max - counter;

                counter++;
            }

            return gBand;
        }

        public int MakeRenumbering(IEnumerable<IElement> elements)
        {
            var incMatr = GetLocalNodesInc(elements);
            var graph = new Graph(incMatr.Length);

            graph.ConnectVerteces(incMatr);

            var minAdjVerts = graph.GetMinAdjVertices(5);

            var bandList = new List<int>();
            var orderList = new List<List<int>>();
            var gibsDic = new Dictionary<int, int>();
            //var deg = Degrees;
            for (int i = 0; i < minAdjVerts.Count; i++)
            {
                //WriteToLog("\t > Вариант " + i.ToString());

                var gibsInd = graph.GIBs(minAdjVerts[i]);
                var gibsOrder = graph.CHMRenumbering(gibsInd);

                gibsDic = gibsOrder.Select((value, index) => new { Key = value, Value = index })
                                             .ToDictionary(item => item.Key, item => item.Value);

                graph.SetNewOrder(gibsDic);
                bandList.Add(graph.GetBandWidth());
                orderList.Add(gibsOrder);

                graph = new Graph(incMatr.Length);
                graph.ConnectVerteces(incMatr);

            }

            var ind = bandList.IndexOf(bandList.Min());

            //MatrixIndex.BandWidth = 
            gibsDic = orderList[ind].Select((value, index) => new { Key = value, Value = index })
                                                   .ToDictionary(item => item.Key, item => item.Value);
            SetNewOrder(gibsDic);
            return bandList[ind];
        }

        public List<int>[] MakeExclusion_MinDeg(IEnumerable<IElement> elements, int dof)
        {
            var lInc = GetLocalNodesInc(elements);
            var graph = new Graph(lInc.Length);

            graph.ConnectVerteces(lInc);

            var ex = graph.MakeExclusion_2();

            var ex_summ = ex.Item1.Select(x => x.Count).Sum();

            var in_summ = lInc.Select(x => x.Count).Sum();

            graph = new Graph(lInc.Length);
            graph.ConnectVerteces(lInc);
            graph.ConnectVerteces(ex.Item1);

            var rate = ex_summ / in_summ;

            //return GetGlobalNodesInc(elements, dof);

            //var matr = graph.GetSimbSimMatrix();
            var matr = graph.GetIncMatrix();

            var incidentData = new List<int>[dof * nodes.Count];

            for (int i = 0; i < incidentData.Length; i++)
                incidentData[i] = new List<int>();

            var counter = 0;
            foreach (var data in matr)
            {
                var gInds = new List<int>();

                foreach (var item in data)
                    gInds.AddRange(CreateGlobalInds(dof, item));

                if (dof == 2)
                {
                    incidentData[dof * counter + 0] = gInds;
                    incidentData[dof * counter + 1] = gInds;
                }
                else if (dof == 3)
                {
                    incidentData[dof * counter + 0] = gInds;
                    incidentData[dof * counter + 1] = gInds;
                    incidentData[dof * counter + 2] = gInds;
                }
                else
                    incidentData[counter] = gInds;

                counter++;
            }

            return incidentData;
        }


        public List<int>[] MakeExclusion(IEnumerable<IElement> elements, int dof)
        {
            var lInc = GetLocalNodesInc(elements);
            var graph = new Graph(lInc.Length);

            graph.ConnectVerteces(lInc);

            var ex =  graph.MakeExclusion_3();

            var ex_summ = ex.Select(x => x.Count).Sum();

            var in_summ = lInc.Select(x => x.Count).Sum();

            graph.ConnectVerteces(lInc);
            graph.ConnectVerteces(ex);

            var rate = ex_summ/in_summ;

            //return GetGlobalNodesInc(elements, dof);

            // Временный метод
            var matr = graph.GetSimbSimMatrix();
            //var matr = graph.GetIncMatrix();

            var incidentData = new List<int>[dof * nodes.Count];

            for (int i = 0; i < incidentData.Length; i++)
                incidentData[i] = new List<int>();

            var counter = 0;
            foreach (var data in matr)
            {
                var gInds = new List<int>();

                foreach (var item in data)
                    gInds.AddRange(CreateGlobalInds(dof, item));

                if (dof == 2)
                {
                    incidentData[dof * counter + 0] = gInds;
                    incidentData[dof * counter + 1] = gInds;
                }
                else if (dof == 3)
                {
                    incidentData[dof * counter + 0] = gInds;
                    incidentData[dof * counter + 1] = gInds;
                    incidentData[dof * counter + 2] = gInds;
                }
                else
                    incidentData[counter] = gInds;

                counter++;
            }

            return incidentData;
        }


        public List<int>[] GetLocalNodesInc(IEnumerable<IElement> elements)
        {
            //if (elsNdsInds == null) throw new ArgumentNullException(nameof(elsNdsInds));

            //var deg = MatrixIndex.Degrees;
            var incidentData = new List<int>[nodes.Count];

            for (int i = 0; i < incidentData.Length; i++)
            {
                incidentData[i] = new List<int>();
            }


            //found a new new_NoneZeroNodes
            foreach (var element in elements)
            {
                var lInds = element.GetVertexes().Select(x =>
                nodes[x.Number]);

                //предполагаем, что во всех элементах все узлы связаны
                foreach (var masterInd in lInds)
                {
                    foreach (var slaveInd in lInds)
                    {
                        if (!incidentData[masterInd].Contains(slaveInd))
                            incidentData[masterInd].Add(slaveInd);
                    }
                }
            }

            return incidentData;
        }



        public List<int>[] GetGlobalNodesInc(IEnumerable<IElement> elements, int dof)
        {
            //if (MatrixIndex == null) throw new ArgumentNullException(nameof(MatrixIndex));


            var incidentData = new List<int>[dof * nodes.Count];

            for (int i = 0; i < incidentData.Length; i++)
            {
                incidentData[i] = new List<int>();
            }
            //var length = elements.Count();
            //found a new new_NoneZeroNodes
            foreach (var element in elements)
            {
                var gInds = GetGlobalInds(element, dof);

                foreach (var masterInd in gInds)
                {
                    foreach (var slaveInd in gInds)
                    {
                        if (!incidentData[masterInd].Contains(slaveInd))
                            incidentData[masterInd].Add(slaveInd);
                    }
                }
            }

            return incidentData;
        }


        public List<int> GetGlobalInds(IElement element, int dof)
        {
            var numbers = element.GetVertexes().Select(x =>
x.Number);

            var list = new List<int>();

            foreach (var number in numbers)
            {
                var nInd = nodes[number];
                //var snInd = LinearSearch.Linear(order, nInd);

                list.AddRange(CreateGlobalInds(dof, nInd));
            }
            // С сортировкой нарушается естественный порядок индексов.Но это тоже вопрос
            //list.Sort();
            return list;
        }

        private List<int> CreateGlobalInds(int dof, int nInd)
        {
            var list = new List<int>();

            if (dof == 1)
            {
                // массив коэффициентов для формирования глобальной 3D матрицы жесткости
                list.Add(nInd);
            }
            else if (dof == 2)
            {

                var indX = (2 * nInd) + 0;
                var indY = (2 * nInd) + 1;

                // массив коэффициентов для формирования глобальной 3D матрицы жесткости
                list.Add(indX); list.Add(indY);
            }
            else
            {
                var indX = (3 * nInd) + 0;
                var indY = (3 * nInd) + 1;
                var indZ = (3 * nInd) + 2;
                // массив коэффициентов для формирования глобальной 3D матрицы жесткости
                list.Add(indX); list.Add(indY); list.Add(indZ);
            }

            return list;
        }
    }
}
