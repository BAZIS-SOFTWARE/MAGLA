using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static IronPython.Modules._ast;

namespace TaskSolverCore.Utilities
{
    public class Graph : IEnumerable<Vertex>
    {
        private List<Vertex> verts;

        public List<int>[] GetIncMatrix()
        {
            var incMatr = new List<int>[verts.Count];
            for (int i = 0; i < incMatr.Length; i++)
            {
                incMatr[i] = new List<int>();
                foreach (var incVert in verts[i].IncidentVerts)
                    incMatr[i].Add(incVert.Index);
            }
            return incMatr;
        }

        public Graph(int nodesCount)
        {
            verts = Enumerable.Range(0, nodesCount).Select(z => new Vertex(z)).ToList();
        }

        public void SetNewOrder(Dictionary<int,int> order)
        {
            foreach (var item in order)
            {
                this[item.Key].ChangeIndex(item.Value);
            }
            //for (int i = 0; i < order.Count; i++)
            //    nodes[i] = order[i];
        }

        public void ConnectVerteces(List<int>[] incMatrix)
        {
            //this.incMatrix = incMatrix;
            for (int i = 0; i < incMatrix.Length; i++)
            {
                foreach (var incVert in incMatrix[i])
                {
                    Connect(i, incVert);
                }
            }
        }

        public void ConnectVerteces(HashSet<int>[] incMatrix)
        {
            //this.incMatrix = incMatrix;
            for (int i = 0; i < incMatrix.Length; i++)
            {
                foreach (var incVert in incMatrix[i])
                {
                    Connect(i, incVert);
                }
            }
        }
        /// <summary>
        /// Length
        /// </summary>
        public int Length { get { return verts.Count; } }

        public Vertex this[int index] { get { return verts[index]; } }
        /// <summary>
        /// Connect
        /// </summary>
        /// <param name="index1"></param>
        /// <param name="index2"></param>
        public void Connect(int index1, int index2)
        {
            verts[index1].Connect(verts[index2]);
        }

        public void Disconnect(int index1, int index2)
        {
            verts[index1].Disconnect(verts[index2]);
        }


        public List<int> GetMinAdjVertices(int foundNumbrs)
        {
            var minAdjVerts = new List<int>();

            var vertInd = 0;
            var count = 0;
            var startIndex = 0;
            var stopIndex = 0;

            if (Length > 5) stopIndex = Length / foundNumbrs; 
            else { stopIndex = Length; }

            while (true)
            {
                vertInd = startIndex;
                count = verts[startIndex].IncidentVerts.Count();
                for (int i = startIndex; i < stopIndex; i++)
                {
                    if (verts[i].IncidentVerts.Count() < count)
                    {
                        vertInd = i;
                        count = verts[i].IncidentVerts.Count();
                    }
                }
                if(!minAdjVerts.Contains(vertInd))
                    minAdjVerts.Add(vertInd);

                startIndex = stopIndex;
                if (startIndex == Length) break;
                stopIndex = stopIndex + stopIndex;
                if (stopIndex > Length) stopIndex = Length;
            }
            return minAdjVerts;
        }
        [Obsolete("Лучше построить новый граф и получить матрицу инциденций")]
        public List<int>[] GetRenumIncMatrix(List<int> order)
        {
            var newIncMatrix = new List<int>[Length];

            for (int j = 0; j < Length; j++)
            {
                var rowInd = j;

                foreach (var vert in this[rowInd].IncidentVerts)
                {
                    var colInd = order.IndexOf(vert.Index);
                    if (newIncMatrix[rowInd] == null)
                        newIncMatrix[rowInd] = new List<int>();
                    newIncMatrix[rowInd].Add(colInd);
                }

                //for (int k = 0; k < this[j].IncidentVerts.Count(); k++)
                //{
                //    var colInd = Search.Linear(order, this[j].VertexNumber);
                //    if (newIncMatrix[rowInd] == null)
                //        newIncMatrix[rowInd] = new List<int>();
                //    newIncMatrix[rowInd].Add(colInd);
                //}
            }

            return newIncMatrix;
        }

        public IEnumerable<Vertex> DepthSearch_Correct(Vertex startNode)
        {
            var visited = new HashSet<Vertex>();
            var stack = new Stack<Vertex>();
            visited.Add(startNode);
            stack.Push(startNode);
            while (stack.Count != 0)
            {
                var node = stack.Pop();
                yield return node;
                foreach (var nextNode in node.IncidentVerts.Where(n => !visited.Contains(n)))
                {
                    visited.Add(nextNode);
                    stack.Push(nextNode);
                }
            }
        }

        public List<Vertex> FindPath(Vertex start, Vertex end)
        {
            var track = new Dictionary<Vertex, Vertex>();
            track[start] = null;
            var queue = new Queue<Vertex>();
            queue.Enqueue(start);
            while (queue.Count != 0)
            {
                var vertex = queue.Dequeue();
                foreach (var nextNode in vertex.IncidentVerts)
                {
                    if (track.ContainsKey(nextNode)) continue;
                    track[nextNode] = vertex;
                    queue.Enqueue(nextNode);
                }
                if (track.ContainsKey(end)) break;
            }
            var pathItem = end;
            var result = new List<Vertex>();
            while (pathItem != null)
            {
                result.Add(pathItem);
                pathItem = track[pathItem];
            }
            result.Reverse();
            return result;
        }

        public List<int> ParralelSections(List<List<int>> rls)
        {
            var n = rls.Count;

            var m = Length / n;
            var k = (int)((n * Math.Sqrt(2)) / (Math.Sqrt(3) * m));

            if (k == 0)
                k = 1;
            var p = (n - 2) / k;

            var order = new List<int>();

            var splittersInds = new List<int>();
            var splittersOrder = new List<int>();

            for (int i = 0; i <= k; i++)
            {
                var ind = 1 + i * p;
                splittersInds.Add(ind);
                splittersOrder.AddRange(rls[ind]);
            }

            order.AddRange(rls[0]);
            for (int i = 1; i < n; i++)
            {
                if (!splittersInds.Contains(i))
                    order.AddRange(rls[i]);
            }
            order.AddRange(splittersOrder);
            return order;
        }

        public int GIBs(int startInd)
        {
            var firstRlsLength = 0;
            var secondRlsLength = 0;

            var firstInd = startInd;
            var secondNumber = 0;

            var rls = new List<List<int>>();

            var deg = new Dictionary<int, int>();

            while (true)
            {
                if (rls.Count == 0)
                {
                    rls = GetLevelStructure(firstInd);
                    firstRlsLength = rls.Count();
                }

                foreach (var ind in rls.Last())
                {
                    var count = verts[ind].IncidentVerts.Count();
                    if(!deg.ContainsKey(ind))
                        deg.Add(ind, count );
                }
                var minDeg = deg.Values.Min();
                secondNumber = deg.FirstOrDefault(x => x.Value == minDeg).Key;
                rls = GetLevelStructure(secondNumber);
                secondRlsLength = rls.Count();

                if (firstRlsLength >= secondRlsLength) 
                    return firstInd;
                else
                {
                    firstInd = secondNumber;
                    firstRlsLength = secondRlsLength;
                    deg.Clear();
                }
            }
        }

        public List<List<int>> GetLevelStructure(int startInd)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>();

            var levs = new List<List<int>>();
            var lev = new List<int>() { startInd };

            levs.Add(lev);

            var levNumb = 0;

            visited.Add(startInd);
            queue.Enqueue(startInd);
            while (queue.Count != 0)
            {
                var parentInd = queue.Dequeue();
                
                foreach (var nextVertex in verts[parentInd].IncidentVerts)
                {
                    // вот тут кажется ошибка
                    //var childNumber = nextVertex.Number;

                    var childInd = nextVertex.Index;

                    if (!visited.Contains(childInd))
                    {
                        visited.Add(childInd);
                        queue.Enqueue(childInd);

                        var parentLevelNumb = FindVertexLevel(parentInd, levNumb,levs);
                        if (parentLevelNumb + 1 > levNumb)
                        {
                            levNumb++;
                            levs.Add(new List<int>() { childInd });

                        }
                        else levs[parentLevelNumb + 1].Add(childInd);
                    }

                }

            }
            return levs;
        }     

        public List<int> GetReachVertexes(int startNumb)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            var reach = new List<int>();

            visited.Add(startNumb);
            queue.Enqueue(startNumb);
            while (queue.Count != 0)
            {
                var parentNumb = queue.Dequeue();

                foreach (var nextVertex in verts[parentNumb].IncidentVerts)
                {
                    var childIndex = nextVertex.Index;
                    if (!visited.Contains(childIndex))
                    {
                        visited.Add(childIndex);

                        if ( verts[childIndex].Mark == Mark.Visited)
                            queue.Enqueue(childIndex);
                        else reach.Add(childIndex);
                    }
                }
            }
            return reach.Distinct().ToList();
        }

        private int FindVertexLevel(int searchedNumb, int levNumber, List<List<int>> levs)
        {
            int resu = -1;

            var searchedLev = levNumber - 1;
            if (searchedLev < 0) searchedLev = 0;

            for (int i = searchedLev; i < levs.Count; i++)
            {
                if (levs[i].Contains(searchedNumb))
                {
                    resu = i;
                    break;
                }                  
            }

            if (resu == -1) throw new Exception("Root node was not found!");
            return resu;
        }
        /// <summary>
        /// Реализация исключения прямо на графе
        /// </summary>
        /// <returns></returns>
        public HashSet<int>[] MakeExclusion_3()
        {
            var fillInd = new HashSet<int>[Length];

            for (int i = 0; i < Length; i++)
                fillInd[i] = new HashSet<int>();

            for (int i = 0; i < Length; i++)
            {
                var skip = 1;
                foreach (var master in verts[i].IncidentVerts)
                {
                    foreach (var slave in verts[i].IncidentVerts.Skip(skip))
                    {
                        if (master.Connect(slave))
                        {
                            if (master.Index < slave.Index)
                                fillInd[master.Index].Add(slave.Index);
                            else
                                fillInd[slave.Index].Add(master.Index);
                        }


                    }
                    skip++;
                }

                foreach (var item in verts[i].IncidentVerts)
                    item.Disconnect(verts[i]);
            }

            return fillInd;
        }

        /// <summary>
        /// Реализация исключения прямо на графе с поиском узла мин.степени
        /// </summary>
        /// <returns></returns>
        public Tuple<HashSet<int>[], HashSet<int>> MakeExclusion_2()
        {
            var fillInd = new HashSet<int>[Length];

            for (int i = 0; i < Length; i++)
                fillInd[i] = new HashSet<int>();

            var exList = new HashSet<int>();

            while (true)
            {
                var ind = FindMinIncVert();
                exList.Add(verts[ind].Index);

                var skip = 1;
                foreach (var master in verts[ind].IncidentVerts)
                {
                    foreach (var slave in verts[ind].IncidentVerts.Skip(skip))
                    {
                        if (master.Connect(slave))
                        {
                            if (master.Index < slave.Index)
                                fillInd[master.Index].Add(slave.Index);
                            else
                                fillInd[slave.Index].Add(master.Index);
                        }


                    }
                    skip++;
                }

                foreach (var item in verts[ind].IncidentVerts)
                    item.Disconnect(verts[ind]);

                verts.Remove(verts[ind]);

                if(Length == 1)
                {
                    exList.Add(verts[0].Index);
                    break;
                }

            }

            return new Tuple<HashSet<int>[], HashSet<int>>
                (fillInd,exList);
        }

        public int FindMinIncVert()
        {
            var pivotInd = 0;
            var deg = 0;
            var counter = 0;
            foreach (var item in verts)
            {
                var count = item.IncidentVerts.Count();

                if (deg == 0 | count < deg)
                {
                    deg = count;
                    pivotInd = counter;
                }
                counter++;
            }

            return pivotInd;
        }

        //public List<int>[] MakeExclusion_1()
        //{
        //    var upInd = GetUpDiagIndexes();
        //    var reaches = new List<int>[Length];

        //    var numberOfReaches = 0;

        //    for (int i = 0; i < Length; i++)
        //    {
        //        var reach = new HashSet<int>();

        //        foreach (var item in upInd[i])
        //            reach.Add(item);


        //        for (int j = 0; j < numberOfReaches; j++)
        //        {
        //            if (reaches[j].BinarySearch(i) != 0)
        //            {
        //                foreach (var item in reaches[j]) // первый не берем
        //                {
        //                    // проверку можно не вести так как hashSet
        //                    if (!reach.Contains(item))
        //                        reach.Add(item);
        //                }
        //            }
        //        }

        //        var remove = reach.Where(x => x >= i).ToList();

        //        //for (int j = 0; j < reach.Count; j++)
        //        //{
        //        //    if (reach[j] < i)
        //        //    {
        //        //        reach.Remove(reach[j]);
        //        //        j--;
        //        //    }
        //        //}

        //        remove.Sort();
        //        reaches[i] = remove;
        //        numberOfReaches++;
        //    }


        //    return reaches;
        //}

        public List<int> MinIncRenumbering_m()
        {
            var exList = new List<int>();
            //2,4,7,5,9,8,0,1,3,6
            var incMatrix = GetIncMatrix();
            var verts = Enumerable.Range(0, Length).ToList();
            while (true)
            {
                var length = verts.Count;

                var pivotVert = 0;
                var deg = 0;

                for (int i = 0; i < length; i++)
                {
                    var tempVert = verts[i];
                    var count = incMatrix[tempVert].Count();

                    if (deg == 0 | count < deg)
                    {
                        deg = count;
                        pivotVert = verts[i];
                    }
                }

                for (int i = 0; i < incMatrix[pivotVert].Count; i++)
                {
                    var adjVert = incMatrix[pivotVert][i];
                    incMatrix[adjVert].AddRange(incMatrix[pivotVert]);
                    incMatrix[adjVert].Remove(pivotVert);
                    incMatrix[adjVert].Remove(adjVert);
                    incMatrix[adjVert] = incMatrix[adjVert].Distinct().ToList();
                }
                incMatrix[pivotVert].Clear();
                verts.Remove(pivotVert);
                exList.Add(pivotVert);
                if (exList.Count == Length)
                {
                    break;
                }
            }
            return exList;
        }

        public List<int> MinIncRenumbering()
        {
            var exList = new List<int>();

            while (true)
            {

                var length = verts.Count;
                var vertInd = 0;
                var deg = 0;

                for (int i = 0; i < length; i++)
                {
                    if (verts[i].Mark == Mark.NotVisited)
                    {
                        verts[i].Mark = Mark.Visited;
                        var reachSet = GetReachVertexes(i);

                        if (deg == 0 | reachSet.Count < deg)
                        {
                            deg = reachSet.Count;
                            vertInd = i;
                        }

                        verts[i].Mark = Mark.NotVisited;
                    }
                }

                verts[vertInd].Mark = Mark.Visited;
                exList.Add(vertInd);
                if (exList.Count == Length)
                {
                    break;
                }
            }
            return exList;
        }
        /// <summary>
        /// GetSimbSimMatrix(
        /// </summary>
        /// <returns></returns>
        [Obsolete("Не использовать. Если нужны связи между узлами работать через матрицу инциденций")]
        public List<int>[] GetSimbSimMatrix()
        {
            var inc = GetIncMatrix();

            var ui = new List<int>[Length];
            var loverRange = new List<int>();

            for (int i = 0; i < Length; i++)
            {
                loverRange.Add(i);

                ui[i] = new List<int>() { i };
                var diff = inc[i].Except(loverRange).ToList();

                // пока не отсортируем???
                diff.Sort();
                ui[i].AddRange(diff);
            }

            return ui;
        }

        public void Clear()
        {
            verts.Clear();
        }

        private void Delete(int verNumber)
        {
            var incVerts = verts[verNumber];

            foreach (var item in incVerts.IncidentVerts)
                item.Disconnect(incVerts);

            verts.Remove(incVerts);        
        }     

        public int GetBandWidth()
        {
            // bandWidth
            var bandWidth = 0;
            var resu = 0;
            for (int i = 0; i < verts.Count; i++)
            {
                if (verts[i].IncidentVerts.Count() == 0)
                    continue;

                var max = verts[i].IncidentVerts.Max(x => x.Index);
                var min = verts[i].Index;

                resu = max - min;
                if (resu > bandWidth)
                    bandWidth = resu;
            }
            return bandWidth + 1;
        }

        public List<int> CHMRenumbering(int startVertInd)
        {
            var q = new List<int>();
            var r = new List<int>();

            //,int startNode, List< int >[] matrixGraph

            r.Add(startVertInd);
            //start

            foreach (var vert in verts[startVertInd].IncidentVerts)
            {
                q.Add(vert.Index);
            }

            // continious
            for (int j = 0; j < q.Count; j++)
            {
                var currentIndex = q[j];

                if (!r.Contains(currentIndex))
                {
                    r.Add(currentIndex);
                    foreach (var vert in verts[currentIndex].IncidentVerts)
                    {
                        q.Add(vert.Index);
                    }
                }

                q.Remove(currentIndex); j--;


                if (r.Count == Length) break;
            }

            r.Reverse();
            return r;
        }

        public IEnumerator<Vertex> GetEnumerator()
        {
            foreach (var item in verts)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
