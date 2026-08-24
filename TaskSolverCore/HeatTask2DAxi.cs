using Model.Interfaces;
using Project.Interfaces;
using Project.Interfaces.Tasks;
using Project.TaskParameters;
using TaskSolverCore.BoundaryConditions;
using TaskSolverCore.ElementData;


namespace TaskSolverCore
{
    public class HeatTask2DAxi : HeatTask
    {
        public HeatTask2DAxi(int index, string folder, ITaskData taskData, TermalParameters parameters, HeatTransportOptions? transportOptions = null, bool? convection = null) :
            base(index, folder, taskData, parameters, transportOptions, convection)
        {
            BoundaryCalculator = new HeatBoundary2DAxi();
        }
/// <inheritdoc/>

        public override void CheckLoadAndBoundaryConditions(NodeDofMap nodesData, ElementsData<ElementTermal> elementsData)
        {
   
            var heatData = HeatData.Where(x => x.Group.ObjType == ObjType.Элемент2D);
            CheckLoadData(nodesData, elementsData, heatData);
            var els1DMedData = MediaData.Where(x => x.Group.ObjType == ObjType.Элемент1D);
            CheckBoundaryData(nodesData, els1DMedData);
            var mediaData = MediaData.Where(x => x.Group.ObjType == ObjType.Узел);
            CheckLoadData(nodesData, elementsData, mediaData);
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
