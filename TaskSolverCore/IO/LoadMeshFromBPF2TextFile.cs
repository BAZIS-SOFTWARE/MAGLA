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
using Model.ObjectsCollections;
using Model.Interfaces.MeshObjects;
using System.Drawing;

namespace Model.IO
{
    /// <summary>
    /// LoadModelFromProjectTextFile
    /// </summary>
    public class LoadMeshFromBPF2TextFile : IModelLoader
    {
        public event Action<object, ILoaderEventArgs> LoadEvent;

        public IModelData Load(string path)
        {
            using (var sr = new StreamReader(path))
            {
                var newModel = new ModelData();
                var rnd = new Random();
                string[] token = new string[1];
                while (!sr.EndOfStream)
                {
                    if (token[0] == "Узел")
                    {
                        LoadEvent?.Invoke(this, new LoaderEventArgs("Загрузка узлов..."));
                        Thread.Sleep(100);
                        LoadNodes(sr, newModel);
                    }
                    if (token[0] == "Элемент3D")
                    {
                        var color = Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256));
                        var set = new ObjectsSet<Element3D>(token[1]);
                        set.SetColor(color);
                        FillSet(sr, set, newModel);
                        newModel.ObjectData.E3DCollection.Add(set);
                    }
                    if (token[0] == "Элемент2D")
                    {
                        var color = Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256));
                        var set = new ObjectsSet<Element2D>(token[1]);
                        set.SetColor(color);
                        FillSet(sr, set, newModel);
                        newModel.ObjectData.E2DCollection.Add(set);
                    }
                    if (token[0] == "Элемент1D")
                    {
                        var color = Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256));
                        var set = new ObjectsSet<Beam>(token[1]);
                        set.SetColor(color);
                        FillSet(sr, set, newModel);
                        newModel.ObjectData.E1DCollection.Add(set);
                    }

                    if (token[0] == "Группы")
                    {
                        LoadEvent?.Invoke(this, new LoaderEventArgs("Загрузка групп..."));
                        Thread.Sleep(100);
                        LoadGroup(sr, newModel);
                    }
                    token = sr.ReadLine().Split(' ');
                }

                LoadEvent?.Invoke(this, new LoaderEventArgs("Модель загружена"));
                Thread.Sleep(100);
                return newModel;
            }


        }

        private static void LoadNodes(StreamReader reader, ModelData newMesh)
        {
            var line = reader.ReadLine();
            //var nodes = new List<Node>();
            while (line != "#Узел")
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
            //newMesh.ObjectData.NodeCollection.AddRange(ObjType.Узел.ToString(), nodes.OrderBy(n => n.Number));
        }

        private void FillSet<T>(StreamReader reader, ObjectsSet<T> set, ModelData newMesh)
            where T : IElement
        {
            LoadEvent?.Invoke(this, new LoaderEventArgs($"Загрузка набора {set.Name}..."));
            Thread.Sleep(100);

            var line = reader.ReadLine();

            while (line != $"#{set.ObjType}")
            {
                var tokens = line.Split()
.Where(m => int.TryParse(m, out var val))
.Select(int.Parse);
                var elementNumber = tokens.First();
                var level = 2;// tokens.Skip(1).First();

                var nodes = tokens.Skip(2).Select(x => newMesh.ObjectData.NodesSet[x]).ToArray();

                IElement elem;
                if (typeof(T) == typeof(Element3D))
                    elem = GetElement3D(nodes, elementNumber, level);
                else if (typeof(T) == typeof(Element2D))
                    elem = GetElement2D(nodes, elementNumber, level);
                else
                    elem = new Beam(elementNumber, nodes.ToArray()) { Level = level };

                set.Add(elem.Number, (T)elem);
                line = reader.ReadLine();
            }
        }


        private Element3D GetElement3D(Node[] nodes, int elementNumber, int level)
        {
            switch (nodes.Length)
            {
                case 4: return new Tetra_c(elementNumber, nodes) { Level = level };
                case 6: return new Penta(elementNumber, nodes) { Level = level };
                case 8: return new Hexa(elementNumber, nodes) { Level = level };
                default: throw new ArgumentException($"Undefined element {elementNumber}");
            }
        }

        private Element2D GetElement2D(Node[] nodes, int elementNumber, int level)
        {
            switch (nodes.Length)
            {
                case 3: return new Triangle_c(elementNumber, nodes) { Level = level };
                case 4: return new Quad(elementNumber, nodes) { Level = level };
                default: throw new ArgumentException($"Undefined element {elementNumber}");
            }
        }

        private static void LoadGroup(StreamReader sr, ModelData newMesh)
        {
            var line = sr.ReadLine();
            while (line != "#Группы")
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
                var indexes = indexLine.Split(' ').Select(x => int.Parse(x));

                foreach (var index in indexes)
                {
                    var obj = newMesh.ObjectData.Find(objType, index);
                    newGroup.Add(obj);
                }

                newMesh.GroupData.Add(newGroup);
            }
        }
    }
}
