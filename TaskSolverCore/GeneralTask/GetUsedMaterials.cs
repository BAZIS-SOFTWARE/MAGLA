//using PrFunctionLib;

using System.Data;
using Model.Interfaces.MeshObjects;
using Project.Tasks;
using MaterialDB.MaterialData;

namespace TaskSolverCore
{
    public abstract partial class GeneralTask
    {
        public Dictionary<MaterialDBItem, List<IElement>> GetUsedMaterials(IEnumerable<MatData> mats)
        {
            var matsDic = new Dictionary<MaterialDBItem, List<IElement>>();
            foreach (var mat in mats)
            {
                //if(!materialDB.ContainsKey(mat.MatName))
                //    throw new Exception($"Материал {mat} отсутсвует в базе!");

                if (!matsDic.ContainsKey(mat.Material))
                    matsDic.Add(mat.Material, mat.Group.Select(x => (IElement)x).ToList());
                else
                    matsDic[mat.Material].AddRange(mat.Group.Select(x => (IElement)x));
            }
            return matsDic;
        }
    }
}
