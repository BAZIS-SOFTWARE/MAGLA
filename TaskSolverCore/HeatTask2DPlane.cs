
using Project.Interfaces;
using Model.Interfaces;
using Project.TaskParameters;
using TaskSolverCore.ElementData;
using Project.Interfaces.Tasks;

namespace TaskSolverCore
{
    public class HeatTask2DPlane : HeatTask
    {
        public HeatTask2DPlane(int index, string folder,ITaskData taskData, TermalParameters parameters, HeatTransportOptions? transportOptions = null, bool? convection = null)
            : base(index, folder, taskData, parameters, transportOptions, convection)
        {
            BoundaryCalculator = new BoundaryConditions.HeatBoundary2DPlane();
        }


        public override void CheckLoadAndBoundaryConditions(NodeDofMap geo, ElementsData<ElementTermal> elementsData)
        {
            var els2DHeatData = HeatData.Where(x => x.Group.ObjType == ObjType.Элемент2D);
            CheckLoadData(geo, elementsData,els2DHeatData);
            var els1DMedData = MediaData.Where(x => x.Group.ObjType == ObjType.Элемент1D);
            CheckBoundaryData(geo, els1DMedData);
            var nodesData = MediaData.Where(x => x.Group.ObjType == ObjType.Узел);
            CheckLoadData(geo, elementsData, nodesData);
        }

        //public override Tuple<Matrix<float>, Vector<float>> HeatExchangeData_Calc(IElement elem, float hExch, float mediaTemp)
        //{
        //    var beam = (IElement1D)elem;
        //    var enCoords = beam.GetVertexes().Select(x => x.Position).ToArray();

        //    float length = beam.CalcLength();

        //    var q = Vector<float>.Build.DenseOfArray(new float[] { enCoords[0]._x, enCoords[1]._x });

        //    var mHeatExch = Matrix<float>.Build.DenseOfRowArrays(new float[][]
        //    {
        //            new float[] { 2, 1},
        //            new float[] { 1, 2}
        //    });

        //    mHeatExch = mHeatExch.Multiply((length * hExch * 2 * 3.14f) / 12);

        //    var matr21 = Matrix<float>.Build.DenseOfRowArrays(new float[][]
        //    {
        //            new float[]{2,1},
        //            new float[]{1,2}
        //    });
        //    q = matr21.Multiply(q).Multiply((mediaTemp * hExch * length * 2 * 3.14f) / 6);


        //    return Tuple.Create(mHeatExch, q);

        //}
    }
}
