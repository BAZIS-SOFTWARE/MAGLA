using MaterialDB.MaterialData.MetallurgicalData;
using CAESolvers;
using Model.Interfaces;
using Model.Interfaces.MeshObjects;
using Model.MeshObjects;
using Mono.Unix.Native;
using Project.Interfaces;
using Project.Interfaces.Tasks;
using Project.TaskParameters;
using Project.Tasks;
using Project.Tasks.Functions;
using Project.Tasks.LocalFrames;
using Project.Tasks.Materials;
using PropertiesCalculator.PropertiesController.HardnessModels;
using PropertiesCalculator.PropertiesController.Interfaces;
using PropertiesCalculator.PropertiesController.MetallurgicalModels;
using ResultDB;
using System.Data;
using System.Diagnostics.Metrics;
using TaskSolverCore.BoundaryConditions;
using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;
using TaskSolverCore.Vector;
using static IronPython.Modules._ast;

namespace TaskSolverCore
{
    public abstract partial class HeatTask : GeneralTask<ElementTermal, CSRMatrix>
    {
        public string ChemicalFile { get; set; } = "";
        //public PhaseCalcJMAKModel PhaseCalcJMAKModel { get; }
        public IHeatBoundary BoundaryCalculator { get; internal set; }

        public IHardnessModel HardnessCalculator;

        public MetallurgicalModel MetallurgicalModel;

        internal List<MediaData> MediaData;
        internal List<HeatData> HeatData;

        public TermalConvergence TermalConvergence { get; }
        public HeatTransportOptions TransportOptions { get; }
        public bool Convection { get; }
        internal IHeatConvectionAssembler ConvectionAssembler { get; }
        public HeatTask(int index, string folder,ITaskData taskData, TermalParameters parameters, HeatTransportOptions? transportOptions = null, bool? convection = null) :
            base(index, folder, taskData, parameters, SolverBuilder.CreateHeatTransport(parameters.SolverSettings))
        {
            TaskKind = taskKind.термическая;

            Parameters = parameters;

            TermalConvergence = parameters.TermalConvergence;
            TransportOptions = transportOptions ?? new HeatTransportOptions();
            Convection = convection ?? ReadConvection(parameters);
            TransportOptions.Validate(taskData.TaskType == TaskType.Volume || taskData.TaskType == TaskType.Volume_mixed ? 3 : 2, Convection);
            ConvectionAssembler = new GalerkinHeatConvectionAssembler();

            MediaData = taskData.Find<MediaData>().ToList();
            HeatData = taskData.Find<HeatData>().ToList();

            HardnessCalculator = new KurkinHardnessModels();
            MetallurgicalModel = new MetallurgicalModel();

            Dof = 1;
        }

        private static bool ReadConvection(TermalParameters parameters)
        {
            var property = parameters.GetType().GetProperty("Convection");
            return property?.PropertyType == typeof(bool) && property.GetValue(parameters) is true;
        }
/// <inheritdoc/>

        public override ElementsData<ElementTermal> CreateElementData()
        {
            var elementsNumbers = new HashSet<int>();
            //var nodesNumbers = new List<int>();

            var elementsData = new ElementsData<ElementTermal>();

            foreach (var dataItem in MatData)
            {
                var gr = dataItem.Group;

                var iniTemp = InitialTemp.Count > 0 ?
                    InitialTemp[gr.Name] : 20.0;

                var reactions = dataItem.Material["Металлургия"].PropertyData;
                var phaseTable = dataItem.Material["Общие сведения"]["Структура"].DataTable;

                
         
                var processData = new ProcessData(reactions.Values, Parameters.MetallurgicalProcesses.ToArray());

                foreach (IElement obj in gr)
                {
                    if (elementsNumbers.Contains(obj.Number))
                        throw new Exception($"Элемент с номером {obj.Number} уже используется. Проверьте группы в разделе \"материалы\"");

                    elementsNumbers.Add(obj.Number);

                    //foreach (var node in obj.GetVertexes())
                    //    nodesNumbers.Add(node.Number);

                    ElementTermal eItem;

                    if (TaskType == TaskType.Volume | TaskType == TaskType.Volume_mixed)
                    {
                        if (obj.ObjType == ObjType.Элемент3D)
                        {
                            eItem = new ET3DV((IElement3D)obj);
                        }
                        else if (obj.ObjType == ObjType.Элемент2D)
                        {
                            var plateData = dataItem as PlateMatData;
                            eItem = new ET3DP(plateData.Thickness, (IElement2D)obj);
                        }
                        else
                        {
                            var beamData = dataItem as BeamMatData;
                            eItem = new ET3DB(beamData.Diameter, (Beam)obj);
                        }
                    }
                    else
                    {
                        if (obj.ObjType == ObjType.Элемент2D)
                            eItem = TaskType == TaskType.Plain ? new ET2DPV((IElement2D)obj) : new ET2DAV((IElement2D)obj);
                        else
                        {
                            var plateData = dataItem as PlateMatData;
                            eItem = new ET2DAP(plateData.Thickness, (Beam)obj);
                        }
                    }

                    eItem.Temp = (float)iniTemp;
                    eItem.FusionTemp = Convert.ToSingle(
    dataItem.Material["Общие сведения"]["Кристаллизация"].
    DataTable.Rows[0]["Температура"]);
                    eItem.Material = dataItem.Material.Name;
                    eItem.ProcessData = processData;
                    eItem.PhaseData = new PhaseData(phaseTable);

                    elementsData.Add(eItem);
                }
            }
            return elementsData;
        }

        /// <inheritdoc/>

        //public override void CheckMaterialsProps(List<MaterialDBItem> mats)
        //{
        //    foreach (var mat in mats)
        //        mat.CheckThermalProps();   
        //}

        public override Result CreateIntialResult(float time, ElementsData<ElementTermal> elementsData)
        {
           
            var phaseList = elementsData.GetPhases();
            var dataSet = CreateDataSet(phaseList.ToList());

            var nodesTable = dataSet.Tables["nodes"];

            // Создание строк с результатами по узлам
            var nodesNumbers = elementsData.GetNodesNumbers();

            foreach (var item in nodesNumbers)
            {
                var nodRow = nodesTable.NewRow();
                nodRow["Индекс"] = item;
                nodesTable.Rows.Add(nodRow);
            }


            var elemTable = dataSet.Tables["elements"];

            foreach (var elementItem in elementsData)
            {
                //foreach (var elem in matElems.Value)
                //{
                var eleRow = elemTable.NewRow();
                eleRow["Индекс"] = elementItem.Number;
                eleRow["T"] = elementItem.Temp;

                var phases = elementItem.PhaseData;

                foreach (var phase in phases)
                    eleRow[phase.Name] = Convert.ToSingle(phase.Value);

                elemTable.Rows.Add(eleRow);

                foreach (var nodes in elementItem.Element.GetVertexes())
                {
                    var nodRow = nodesTable.Rows.Find(nodes.Number);

                    nodRow["Индекс"] = nodes.Number;
                    nodRow["T"] = elementItem.Temp;
                    foreach (var phase in phases)
                        nodRow[phase.Name] = Convert.ToSingle(phase.Value);

                }

            }

            return new Result(dataSet, time, TaskKind.ToString());
        }    

        protected override void ApplyBoundaryConditions(
            TaskSystemContext<ElementTermal> context)
        {
            var vec = context.Vectors;
            var matr = context.Matrices;
            var geo = context.Nodes;
            var elemData = context.Elements;
            var time = context.Time;
            var y = vec.GetVectorArray(VectorType.force);
            //var x = vec.GetVectorList();
            var mKC = matr.Get<CSRMatrix>(MatrixType.heatTransferCapacity);
            //var transInds = mKC.TransposeIndexes();
            var last_taskResu = taskResults.Last().Data;

            foreach (var data in MediaData)
            {
                if (time >= data.StartTime & time <= data.StopTime)
                {
                    var refTime = time - data.StartTime;

                    var funcFlag = false;
                    if (data.Function != null)
                        funcFlag = true;

                    if (funcFlag && data.Function.ContainsParameter("TIME"))
                        data.Function["TIME"].SetValue(refTime);

                    var gr = data.Group;

                    if (data.MediaType == MediaType.HeatFlux)
                    {
                        var medTemp = data.Function["TEMPM"].GetValue();
                        foreach (var obj in gr)
                        {
                            var e2Obj = (IElement)obj;
                            var e2Temp = 0.0f;
                            foreach (var node in e2Obj.GetVertexes())
                            {
                                var nInd = geo.IndexOfNode(node.Number);
                                e2Temp += (float)taskResults.Last().Data.Tables["nodes"].Rows[nInd]["T"];
                            }

                            e2Temp /= e2Obj.NumberOfPoints;

                            data.Function["TEMPS"].SetValue(e2Temp);
                            var hExch = data.Function.CalcValue();

                            MediaTemp(vec, matr, e2Obj, geo, hExch, medTemp);
                        }
                    }

                    else
                    {
                        var surfTemp = data.Value;

                        if(funcFlag)
                            surfTemp *= data.Function["TEMPS"].GetValue();
                        foreach (var obj in gr)
                        {
                            var ind = geo.IndexOfNode(obj.Number);
                            mKC.LineCross(y.Vector, surfTemp, ind);
                        }
                    }                                  
                }
            }
        }

        private void MediaTemp(VectorContainer<double> vec, MatrixContainer matr, IElement eObj, NodeDofMap geo, double hExch, double mediaTemp)
        {
            var y = vec.GetVectorArray(VectorType.force);
            //var x = vec.GetVectorList();
            //var nodesNumbers = geo.GetNodesNumbs;

            var mKC = matr.Get<CSRMatrix>(MatrixType.heatTransferCapacity);

            var mHeatExchange = BoundaryCalculator.ExchangeBoundary_Calc(eObj, hExch);
            var q = BoundaryCalculator.FlowBoundary_Calc(eObj, mediaTemp, hExch);
            //var resu = HeatExchangeData_Calc(eObj, hExch, mediaTemp);
            //var mHeatExchange = resu.Item1;

            // определяем глобальные индексы узлов

            var indexes = geo.GetGlobalInds(eObj, Dof);

            // global matrix modification
            for (int k = 0; k < indexes.Count; k++)
            {
                var row = indexes[k];

                y[row] = y[row] + q[k];
                for (int m = 0; m < indexes.Count; m++)
                {
                    var col = indexes[m];
                    mKC.AccumulateAt(row, col, mHeatExchange[k, m]);
                    //if (col >= row)
                    //{
                    //    var scol = 0;
                    //    if (mKC.Kind == MatrixKind.profile)
                    //        scol = mKC.Indexes[row].BinarySearch(col);
                    //    else
                    //        scol = col - row;
                    //    mKC[row, scol] = mKC[row, scol] + mHeatExchange[k, m];
                    //}
                }
            }

        }

        //public abstract Tuple<Matrix<float>, Vector<float>> HeatExchangeData_Calc(IElement elem, float hExch, float mediaTemp);       

        protected override void ApplyLoads(
            TaskSystemContext<ElementTermal> context)
        {
            var vec = context.Vectors;
            var geo = context.Nodes;
            var elemsData = context.Elements;
            var time = context.Time;
            var y = vec.GetVectorArray(VectorType.force);

            foreach (var data in HeatData)
            {

                if (time >= data.StartTime & time <= data.StopTime) //HeatSource(time, line, taskResults.DataSet[timeInd - 1]);
                {
                    var refTime = time - data.StartTime;

                    if (data.LocalFrame != null)
                    {
                        // нужно избавиться от проверки типа (downcast)
                        if (data.LocalFrame is MovedFrame moved)
                            moved.Time = refTime;

                        data.LocalFrame.CalcPosition();
                    }

                    if (data.Function != null && data.Function.ContainsParameter("TIME"))
                        data.Function["TIME"].SetValue(refTime);

                    var gr = data.Group;

                    if (gr.ObjType == ObjType.Узел)
                    {
                        foreach (var obj in gr)
                        {
                            var ind = geo.IndexOfNode(obj.Number);

                            y[ind] = y[ind] + data.Value;
                        }
                    }
                    else
                        foreach (var obj in gr)
                        {
                            var elemData = elemsData[obj.Number];

                            if (elemData.Status == 1)
                            {
                                //вычисляется значение функции
                                var heat = data.Value;

                                var elem = elemData.Element;
                                //пересчет координат если функция координатная
                                if (data.Function != null)
                                {
                                    if (data.Function.FunctionType == FuncType.CPF)
                                    {
                                        var center = elem.CalcCentr();

                                        if (data.LocalFrame != null)
                                            center = data.LocalFrame.Frame.GetCoordsInFrame(center);

                                        data.Function["X"].SetValue(center._x);
                                        data.Function["Y"].SetValue(center._y);
                                        data.Function["Z"].SetValue(center._z);
                                    }

                                    heat *= data.Function.CalcValue();
                                }

                                var enrg_node = elemData.VolumeHeat_Calc(heat);
                                var counter = 0;
                                foreach (var node in elem.GetVertexes())
                                {
                                    var ind = geo.IndexOfNode(node.Number);

                                    y[ind] = y[ind] + enrg_node[counter++];
                                }
                            }

                        }
                }
                               
            }

            ApplyFrictionHeat(context);
        }

        private void ApplyFrictionHeat(TaskSystemContext<ElementTermal> context)
        {
            var options = TransportOptions.FrictionHeat;
            if (!options.Enabled || context.Time < options.StartTime || context.Time > options.StopTime)
                return;

            var groups = MediaData.Select(data => data.Group)
                .Where(group => options.SurfaceGroups.Contains(group.Name, StringComparer.OrdinalIgnoreCase))
                .GroupBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            var missing = options.SurfaceGroups.Except(groups.Select(group => group.Name), StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"Не найдены контактные группы тепловыделения: {string.Join(", ", missing)}.");

            var force = context.Vectors.GetVectorArray(VectorType.force);
            foreach (var group in groups)
            foreach (var obj in group)
            {
                var element = (IElement)obj;
                var velocity = Convection ? TransportOptions.Convection.ResolveVelocity(element.Number, element.GetVertexes().Select(node => node.Number)) : [0.0, 0.0, 0.0];
                var local = BoundaryCalculator.FlowHeat_Calc(element, (x, y, z) => options.CalculateFlux(x, y, z, velocity));
                var indices = context.Nodes.GetGlobalInds(element, Dof);
                for (var index = 0; index < indices.Count; index++)
                    force[indices[index]] += local[index];
            }
        }
      

        protected override void ApplyPreLoads(
            TaskSystemContext<ElementTermal> context)
        {
            var vec = context.Vectors;
            var matr = context.Matrices;
            var geo = context.Nodes;
            var timeStep = context.TimeStep;
            var mC = matr.Get<CSRMatrix>(MatrixType.heatCapacity);
            //var mCm = matr[MatrixType.heatCapacity];
            var x = vec.GetVectorArray(VectorType.result);
            var y = vec.GetVectorArray(VectorType.force);

            var ini_x = new double[geo.Count];

            foreach (var node in geo.GetNodesNumbs)
            {
                var ind = geo.IndexOfNode(node);

                x[ind] = (float)taskResults.Last().Data.Tables["nodes"].Rows[node]["T"];

            }

            //x.Add(ini_x);

            //mtrC.ReduceZeroItems();
            //mtrC.ReduceZeroIndexes();

            double[] preQ = mC.Multiply(x.Vector);
            for (int i = 0; i < preQ.Length; i++)
                preQ[i] /= timeStep;

            y.Sum(preQ,y);
        }

        protected override void SetPhysicalProperties(
            TaskSystemContext<ElementTermal> context)
        {
            var elemsData = context.Elements;
            var time = context.Time;
            foreach (var data in MatData)
            {
                var gr = data.Group;

                foreach (var obj in gr)
                {
                    var elemData = elemsData[obj.Number];

                    if (time >= data.StartTime & time <= data.StopTime)
                    {
                        elemData.ConvectionVelocity = Convection ? TransportOptions.Convection.ResolveVelocity(elemData.Element.Number, elemData.Element.GetVertexes().Select(node => node.Number)) : [0.0, 0.0, 0.0];
                        var phaseData = elemData.PhaseData;

                        if (Convert.ToInt32(data.Material["Общие сведения"]["Модель материала"].DataTable.Rows[0]["Модель материала"]) == 2)
                        {
                            elemData.HeatTransfer[0] = data.Material["Тепловые свойства"]["Теплопроводность X"].CalcProp(phaseData, elemData.Temp);
                            elemData.HeatTransfer[1] = data.Material["Тепловые свойства"]["Теплопроводность Y"].CalcProp(phaseData, elemData.Temp);
                            elemData.HeatTransfer[2] = data.Material["Тепловые свойства"]["Теплопроводность Z"].CalcProp(phaseData, elemData.Temp);
                        }
                        else 
                        {
                            var htr = data.Material["Тепловые свойства"]["Теплопроводность"].CalcProp(phaseData, elemData.Temp);
                            elemData.HeatTransfer[0] = htr;
                            elemData.HeatTransfer[1] = htr;
                            elemData.HeatTransfer[2] = htr;
                        }
                            
                        elemData.HeatCapacity = data.Material["Тепловые свойства"]["Теплоемкость"].CalcProp(phaseData, elemData.Temp);
                        elemData.Density = data.Material["Тепловые свойства"]["Плотность"].CalcProp(phaseData, elemData.Temp);
                        elemData.Status = 1;
                    }

                    else
                    {
                        elemData.HeatTransfer[0] = 1e-8f;
                        elemData.HeatTransfer[1] = 1e-8f;
                        elemData.HeatTransfer[2] = 1e-8f;
                        elemData.HeatCapacity = 1;
                        elemData.Density = 8.8e-06f;
                        elemData.Status = 0;
                    }
                }
            }
        }

        //public override Tuple<bool, double, float> CheckConvergence(double [] x1, double[] x0, NodeDofMap geo, ElementsData<ElementTermal> mat, float timeStep)
        //{
        //    //var x = vec.GetVectorList();
        //    //var dx_max = MatrixSolvers.Error.Absolute(x0, x1);

        //    var convergenceTemp = true;

        //    if (TermalConvergence.Is_Switched_Tm && dx_max - TermalConvergence.Tm > 1e-4)
        //        convergenceTemp = false;

        //    if (convergenceTemp)
        //        CalcPhase(geo, mat, timeStep, x1);

        //    return new Tuple<bool, double, float>(true, dx_max, 0);
        //}

        protected override TaskIterationResult EvaluateIteration(
            TaskSystemContext<ElementTermal> context,
            double[] solution,
            double solutionChange,
            double solutionMaximum,
            bool matrixUpdateScheduled)
        {
            var converged =
                !TermalConvergence.Is_Switched_Tm ||
                solutionChange - TermalConvergence.Tm <= 1e-4;

            CalcPhase(
                context.Nodes,
                context.Elements,
                context.TimeStep,
                solution);

            return new TaskIterationResult(
                converged,
                true,
                matrixUpdateScheduled,
                solutionChange,
                solutionMaximum,
                0.0);
        }



        private void CalcPhase(NodeDofMap geo, ElementsData<ElementTermal> elemsData, float timeStep, double[] x)
        {
            foreach (var elem in elemsData)
            {
                var preTemp = (float)taskResults.Last().Data.Tables["elements"].Rows.Find(elem.Number)["T"];
                elem.Temp = (float)CalcElementTemp(x, geo.GetGlobalInds(elem.Element, Dof));

                elem.HeatVelocity = (elem.Temp - preTemp) / timeStep;

                MetallurgicalModel.Calc(elem.Temp, timeStep, elem.PhaseData, elem.ProcessData);
            }
        }

        private double CalcElementTemp(double[] nodeTemps, List<int> nInds)
        {
            var temp = 0.0;
            for (int j = 0; j < nInds.Count; j++)
            {
                var ind = nInds[j];
                temp = temp + nodeTemps[ind];
            }
            return temp / nInds.Count;
        } 
/// <inheritdoc/>

        

        public override MatrixContainer FormMatrices(NodeDofMap nodes, IEnumerable<IElement> elements)
        {
            var matrixData = new MatrixContainer();

            matrixData.AddMatrix(
                MatrixType.heatTransfer,
                BuildGeneralMatrix(nodes, elements, Dof));
            matrixData.AddMatrix(
                MatrixType.heatCapacity,
                BuildGeneralMatrix(nodes, elements, Dof));
            if (Convection)
                matrixData.AddMatrix(MatrixType.heatConvection, BuildGeneralMatrix(nodes, elements, Dof));
            matrixData.AddMatrix(
                MatrixType.heatTransferCapacity,
                BuildGeneralMatrix(nodes, elements, Dof));

            return matrixData;
        }

        protected override LinearSystem<CSRMatrix> CreateLinearSystem(TaskSystemContext<ElementTermal> context)
        {
            var matrix = context.Matrices.Get<CSRMatrix>(
                MatrixType.heatTransferCapacity);
            var rightHandSide = context.Vectors
                .GetVectorArray(VectorType.force)
                .Vector
                .ToArray();

            return new LinearSystem<CSRMatrix>(matrix, rightHandSide);
        }

        private static CSRMatrix BuildGeneralMatrix(
            NodeDofMap nodes,
            IEnumerable<IElement> elements,
            int degreesOfFreedom)
        {
            var size = nodes.Count * degreesOfFreedom;
            var builder = new CSRMatrixBuilder(size, size);

            foreach (var element in elements)
            {
                var indices = nodes.GetGlobalInds(
                    element, degreesOfFreedom);

                for (var localRow = 0; localRow < indices.Count; localRow++)
                {
                    for (var localColumn = 0; localColumn < indices.Count; localColumn++)
                    {
                        builder.AddToElement(
                            indices[localRow],
                            indices[localColumn],
                            1.0);
                    }
                }
            }

            var matrix = builder.Build();
            matrix.ClearValues();
            return matrix;
        }

        public override DataSet CreateDataSet(List<string>phasesNames)
        {
            var dataSet = new DataSet();

            var dic = new Dictionary<string, Type>()
            {
                { "Индекс", typeof(int) },
                { "T", typeof(float) },
                { "Q", typeof(float) },
                { "V", typeof(float) }
            };
  
            phasesNames.ForEach(x => dic.Add(x, typeof(float)));

            var nTable = dataSet.Tables.Add("nodes");

            foreach (var column in dic)
            {
                var newColumn = new DataColumn(column.Key, column.Value)
                { DefaultValue = 0};
                nTable.Columns.Add(newColumn);
            }

            var keyN = new DataColumn[1];
            keyN[0] = nTable.Columns[0];
            nTable.PrimaryKey = keyN;

            var eTable = dataSet.Tables.Add("elements");

            foreach (var column in dic)
            {
                var newColumn = new DataColumn(column.Key, column.Value)
                { DefaultValue = 0 };
                eTable.Columns.Add(newColumn);
            }

            var keyE = new DataColumn[1];
            keyE[0] = eTable.Columns[0];
            eTable.PrimaryKey = keyE;

            return dataSet;
        }    

        public override void SaveTaskResults(VectorContainer<double> vec, ElementsData<ElementTermal> mat, NodeDofMap geo, float time)
        {
            var dataSet = new DataSet();

            var nTable = taskResults.Last().Data.Tables["nodes"].Clone();

            var x = vec.GetVectorArray(VectorType.result);
            var y = vec.GetVectorArray(VectorType.force);
            //var nTemp = x.Vector.Last();

            foreach (var nodeNumber in geo.GetNodesNumbs)
            {
                // получаем индекс узла по номеру. Учитывая перенумерацию.
                var ind = geo.IndexOfNode(nodeNumber);

                var workRow = nTable.NewRow();

                workRow["T"] = x[ind];
                workRow["Q"] = y[ind];
                
                workRow["Индекс"] = nodeNumber;
                nTable.Rows.Add(workRow);
            }

            var length = mat.Count();
            var eTable = taskResults.Last().Data.Tables["elements"].Clone();

            foreach (var elem in mat)
            {
                var workRow = eTable.NewRow();

                var enrg = 0.0;
                var inds = geo.GetGlobalInds(elem.Element, Dof);
                for (int j = 0; j < inds.Count; j++)
                {
                    var ind = inds[j];
                    enrg = enrg + y[ind];
                }

                enrg = enrg / inds.Count;

                workRow["Индекс"] = elem.Element.Number;
                workRow["T"] = elem.Temp;
                workRow["Q"] = enrg;
                workRow["V"] = elem.HeatVelocity;

                foreach (var phase in elem.PhaseData)
                    workRow[phase.Name] = phase.Value;

                eTable.Rows.Add(workRow);
            }
            dataSet.Tables.Add(nTable);   
            dataSet.Tables.Add(eTable);
            taskResults.Add(new Result(dataSet,time,TaskKind.ToString()));
        }       

        public override VectorContainer<double> FormVectors(int nmbrNodes, int numbrEls)
        {
            var vec = new VectorContainer<double>();
            vec.AddVector(VectorType.force, nmbrNodes);
            vec.AddVector(VectorType.result, nmbrNodes);
            return vec;
        }

        public override void ClearMatrices(MatrixContainer matrixData)
        {
            matrixData.ClearMatrixes();
        }

        public override void ClearVectorLoads(VectorContainer<double> vec)
        {
            vec.GetVectorArray(VectorType.force).Clear();
        }

        public override void ClearVectorResults(VectorContainer<double> vec)
        {
            vec.GetVectorArray(VectorType.result).Clear();
        }
    }
}
