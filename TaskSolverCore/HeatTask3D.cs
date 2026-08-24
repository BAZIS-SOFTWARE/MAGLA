using Model.Interfaces;
using Project.Interfaces;
using Project.Interfaces.Tasks;
using Project.TaskParameters;
using TaskSolverCore.BoundaryConditions;
using TaskSolverCore.ElementData;

namespace TaskSolverCore
{
    public class HeatTask3D : HeatTask
    {
        public HeatTask3D(int index, string folder,ITaskData taskData, TermalParameters parameters) : 
            base(index, folder, taskData, parameters)
        {
            BoundaryCalculator = new HeatBoundary3D();
        }

        public override void CheckLoadAndBoundaryConditions(NodeDofMap geo, ElementsData<ElementTermal> elementsData)
        {
            var els3DHeatData = HeatData.Where(x => x.Group.ObjType == ObjType.Элемент3D);
            CheckLoadData(geo, elementsData,els3DHeatData);
            var els2DMedData = MediaData.Where(x => x.Group.ObjType == ObjType.Элемент2D);
            CheckBoundaryData(geo, els2DMedData);
            var nodesData = MediaData.Where(x => x.Group.ObjType == ObjType.Узел);
            CheckLoadData(geo, elementsData, nodesData);
        }      
    }
}
