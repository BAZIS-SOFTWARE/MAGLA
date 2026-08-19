using Geometry;
using Model.Events;
using Model.GroupsData;
using Model.MeshObjects;
using Model.Interfaces;
using Model.Interfaces.ObjectsFinders;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Model.IO
{
    /// <summary>
    /// LoadModelFromProjectTextFile
    /// </summary>
    public class LoadMeshFromBPFTextFile : IModelLoader
    {
        public event Action<object, ILoaderEventArgs> LoadEvent;

        public IModelData Load(string path)
        {
            using (var sr = new StreamReader(path))
            {
                var newModel = new ModelData();
                var line = string.Empty;

                while (!sr.EndOfStream)
                {
                    if (line == "Узлы")
                    {
                        LoadEvent?.Invoke(this, new LoaderEventArgs("Загрузка узлов..."));
                        Thread.Sleep(100);
                        LoadNodes(sr, newModel);
                    }
                    if (line == "Элементы 3004" || line == "Элементы 2003" ||
                        line == "Элементы3D" || line == "Элементы2D" ||
                        line == "Элементы1D" || line == "Элемент3D" || line == "Элемент2D" ||
                        line == "Элемент1D" || line == "Элементы 3004" || line == "Элементы 3006" ||
                        line == "Элементы 3008")
                    {
                        LoadEvent?.Invoke(this, new LoaderEventArgs("Загрузка элементов..."));
                        Thread.Sleep(100);
                        LoadElement(sr, line, newModel);
                    }

                    if (line == "Группы узлов и элементов")
                    {
                        LoadEvent?.Invoke(this, new LoaderEventArgs("Загрузка групп..."));
                        Thread.Sleep(100);
                        LoadGroup(sr, newModel);
                    }
                    line = sr.ReadLine();
                }

                LoadEvent?.Invoke(this, new LoaderEventArgs("Модель загружена"));
                Thread.Sleep(100);
                return newModel;
            }


        }

        private static void LoadNodes(StreamReader reader, ModelData newMesh)
        {
            var line = reader.ReadLine();
            var nodes = new List<Node>();
            while (line != "#Узлы")
            {
                var tokens = line.Split()
                    .Where(m => float.TryParse(m, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                    .Select(m => float.Parse(m, CultureInfo.InvariantCulture));
                if (tokens.Count() != 0)
                {
                    var nodeIndex = (int)tokens.First();
                    var coordinates = tokens.Skip(1).ToArray();
                    var position = new Point3D(coordinates[0], coordinates[1], coordinates[2]);
                    var node = new Node(nodeIndex, position);
                    newMesh.ObjectData.NodesSet.Add(node.Number, node);
                }
                line = reader.ReadLine();
            }

            //newMesh.ObjectData.NodeCollection.AddRange(ObjType.Узел.ToString(),nodes);
        }

        private void LoadElement(StreamReader reader, string elementType, ModelData newMesh)
        {
            var line = reader.ReadLine();
            while (line != $"#{elementType}")
            {
                var tokens = line.Split()
                    .Where(m => int.TryParse(m, out var val))
                    .Select(int.Parse);
                var elementNumber = tokens.First();
                var nodes = GetNodes(newMesh, tokens.Skip(1)).ToArray();
                if (elementType == "Элементы3D" | elementType == "Элемент3D" |
                    elementType == "Элементы 3004" | elementType == "Элементы 3006" | elementType == "Элементы 3008")
                {
                    var element = GetElement3D(nodes, elementNumber);
                    newMesh.ObjectData.E3DCollection.Add(elementType, element);
                }
                if (elementType == "Элементы2D" | elementType == "Элемент2D" | elementType == "Элементы 2003")
                {
                    var element = GetElement2D(nodes, elementNumber);
                    newMesh.ObjectData.E2DCollection.Add(elementType, element);
                }
                if (elementType == "Элементы1D" | elementType == "Элемент1D" | elementType == "Элементы 1002")
                {
                    var el2DObj = new Beam(elementNumber, nodes.ToArray(),2);
                    newMesh.ObjectData.E1DCollection.Add(elementType, el2DObj);
                }
                line = reader.ReadLine();
            }
        }

        private IEnumerable<Node> GetNodes(ModelData modelData, IEnumerable<int> indeces)
        {
            foreach (var nodeNumber in indeces)
            {
                yield return modelData.ObjectData.NodesSet[nodeNumber];
            }
        }

        private Element3D GetElement3D(Node[] nodes, int elementNumber)
        {
            switch (nodes.Length)
            {
                case 4: return new Tetra_c(elementNumber, nodes,2);
                case 6: return new Penta(elementNumber, nodes,2);
                case 8: return new Hexa(elementNumber, nodes,2);
                default: throw new ArgumentException($"Undefined element {elementNumber}");
            }
        }

        private Element2D GetElement2D(Node[] nodes, int elementNumber)
        {
            switch (nodes.Length)
            {
                case 3: return new Triangle_c(elementNumber, nodes,2);
                case 4: return new Quad(elementNumber, nodes,2);
                default: throw new ArgumentException($"Undefined element {elementNumber}");
            }
        }

        private static void LoadGroup(StreamReader sr, ModelData newMesh)
        {
            var line = sr.ReadLine();
            while (line != "#Группы узлов и элементов")
            {
                var splitLine = line.Split(':');
                if (splitLine[0] == "Группа узлов ")
                {
                    var name = splitLine[1].Split(' ')[1];
                    var nodeGroup = new Group(name, ObjType.Узел);
                    var indexLine = splitLine[1].Remove(0, name.Length + 2);
                    var indexes = indexLine.Split(' ').Select(x => int.Parse(x)).ToList();

                    foreach (var index in indexes)
                    {
                        var obj = newMesh.ObjectData.Find(ObjType.Узел, index);
                        nodeGroup.Add(obj);
                    }
                    newMesh.GroupData.Add(nodeGroup);
                }
                if (splitLine[0] == "Группа элементов 3D ")
                {
                    LoadElemGroup(newMesh, ObjType.Элемент3D, line);
                }
                if (splitLine[0] == "Группа элементов 2D ")
                {
                    LoadElemGroup(newMesh, ObjType.Элемент2D, line);
                }
                if (splitLine[0] == "Группа элементов 1D ")
                {
                    LoadElemGroup(newMesh, ObjType.Элемент1D, line);
                }
                line = sr.ReadLine();
            }
        }

        private static void LoadElemGroup(ModelData newMesh, ObjType objType, string line)
        {
            var splitLine = line.Split(':');
            var name = splitLine[1].Split(' ')[1];
            var newGroup = new Group(name, objType);
            var indexLine = splitLine[1].Remove(0, name.Length + 2);
            if (indexLine != "")
            {
                var elements = newMesh.ObjectData.GetObjects(objType).ToList();
                var indexes = indexLine.Split(' ').Select(x => int.Parse(x)).ToList();
                indexes.Sort();

                foreach (var index in indexes)
                {
                    var obj = ByNumberFinder.Find(elements, index);
                    newGroup.Add(obj);
                }

                newMesh.GroupData.Add(newGroup);
            }
        }
    }
}
