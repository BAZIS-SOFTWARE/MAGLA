using Model.Interfaces.MeshObjects;
using Project.Interfaces;
using Project.Results;
using PropertiesCalculator.FunctionData;
using PropertiesCalculator.MaterialData;
using System.Data;
using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;
using TaskSolverCore.Vector;

namespace TaskSolverCore
{
    public class ChemTask : GeneralTask
    {
        public ChemTask(int index, IProjectData projectData, Tuple<MaterialDBData, FunctionDBData> dataBases) : base(index, projectData, dataBases)
        {
        }

        public override void ApplyBoundCondition(VectorContainer<double> vectorData, MatrixContainer<double> matrixData, GeomData geomData, ElementsData mat, float time, List<Result> taskResults)
        {
            throw new NotImplementedException();
        }

        public override void ApplyLoads(VectorContainer<double> vec, MatrixContainer<double> matr, GeomData geo, ElementsData mat, float time, List<Result> taskResults)
        {
            throw new NotImplementedException();
        }

        public override void ApplyPreLoads(VectorContainer<double> vec, MatrixContainer<double> matr, GeomData geo, ElementsData mat, float time, float timeStep, int iter, List<Result> taskResu)
        {
            throw new NotImplementedException();
        }

        public override Tuple<bool, double, float> CheckConvergence(VectorContainer<double> vec, GeomData geo, ElementsData mat, int iter, float timeStep, List<Result> taskResults)
        {
            throw new NotImplementedException();
        }

        public override void CheckFunctionsData()
        {
            throw new NotImplementedException();
        }

        public override void CheckLoadAndBoundaryConditions(GeomData geo)
        {
            throw new NotImplementedException();
        }

        public override void CheckMaterialsProps(List<string> mats)
        {
            throw new NotImplementedException();
        }

        public override void ClearMatrices(MatrixContainer<double> matrixData)
        {
            throw new NotImplementedException();
        }

        public override void ClearVectorLoads(VectorContainer<double> vec)
        {
            throw new NotImplementedException();
        }

        public override void ClearVectorResults(VectorContainer<double> vec)
        {
            throw new NotImplementedException();
        }

        public override DataSet CreateDataSet(List<string> phasesNames)
        {
            throw new NotImplementedException();
        }

        public override Result CreateIntialResult(float time, Dictionary<string, List<IElement>> matsElems)
        {
            throw new NotImplementedException();
        }

        public override void FillMatrices(MatrixContainer<double> matr, ElementsData elemData, GeomData geo, float timeStep)
        {
            throw new NotImplementedException();
        }

        public override MatrixContainer<double> FormMatrices(GeomData geo)
        {
            throw new NotImplementedException();
        }

        public override VectorContainer<double> FormVectors(int numbrNodes, int numbrElems)
        {
            throw new NotImplementedException();
        }

        public override void SaveTaskResults(VectorContainer<double> vec, ElementsData mat, GeomData geo, float time, List<Result> taskResults)
        {
            throw new NotImplementedException();
        }

        public override void SetPhysicalProp(GeomData geo, ElementsData mat, float time, List<Result> taskResults)
        {
            throw new NotImplementedException();
        }
    }
}
