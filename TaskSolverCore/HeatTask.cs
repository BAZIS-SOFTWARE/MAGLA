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
    public abstract partial class HeatTask : GeneralTask<ElementTermal>
    {
        public string ChemicalFile { get; set; } = "";
        //public PhaseCalcJMAKModel PhaseCalcJMAKModel { get; }
        public IHeatBoundary BoundaryCalculator { get; internal set; }

        public IHardnessModel HardnessCalculator;

        public MetallurgicalModel MetallurgicalModel;

        internal List<MediaData> MediaData;
        internal List<HeatData> HeatData;

        public TermalConvergence TermalConvergence { get; }
        public HeatTask(int index, string folder,ITaskData taskData, TermalParameters parameters) : 
            base(index, folder, taskData, parameters)
        {
            TaskKind = taskKind.термическая;

            Parameters = parameters;

            TermalConvergence = parameters.TermalConvergence;

            MediaData = taskData.Find<MediaData>().ToList();
            HeatData = taskData.Find<HeatData>().ToList();

            HardnessCalculator = new KurkinHardnessModels();
            MetallurgicalModel = new MetallurgicalModel();

            Dof = 1;
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
                            eItem = new ET2DAV((IElement2D)obj);
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

        public override void ApplyBoundCondition(VectorContainer<double> vec, MatrixContainer matr, NodesData geo, ElementsData<ElementTermal> elemData,float time)
        {
            var y = vec.GetVectorArray(VectorType.force);
            //var x = vec.GetVectorList();
            SymmetricCSRMatrix mKC =
                matr.Get<SymmetricCSRMatrix>(MatrixType.heatTransferCapacity);
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

        private void MediaTemp(VectorContainer<double> vec, MatrixContainer matr, IElement eObj, NodesData geo, double hExch, double mediaTemp)
        {
            var y = vec.GetVectorArray(VectorType.force);
            //var x = vec.GetVectorList();
            //var nodesNumbers = geo.GetNodesNumbs;

            SymmetricCSRMatrix mKC =
                matr.Get<SymmetricCSRMatrix>(MatrixType.heatTransferCapacity);

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
                    if (col >= row)
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

        public override void ApplyLoads(VectorContainer<double> vec, MatrixContainer matr, NodesData geo, ElementsData<ElementTermal> elemsData,float time)
        {
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
        }
      

        public override void ApplyPreLoads(VectorContainer<double> vec, MatrixContainer matr, NodesData geo, ElementsData<ElementTermal> mat, float time, float timeStep)
        {
            SymmetricCSRMatrix mC =
                matr.Get<SymmetricCSRMatrix>(MatrixType.heatCapacity);
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

        public override void SetPhysicalProp(NodesData geo, ElementsData<ElementTermal> elemsData, float time)
        {
            foreach (var data in MatData)
            {
                var gr = data.Group;

                foreach (var obj in gr)
                {
                    var elemData = elemsData[obj.Number];

                    if (time >= data.StartTime & time <= data.StopTime)
                    {
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

        //public override Tuple<bool, double, float> CheckConvergence(double [] x1, double[] x0, NodesData geo, ElementsData<ElementTermal> mat, float timeStep)
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

        public override bool CheckMaxDeltaResu(double dx_max)
        {
            //var x = vec.GetVectorList();
            //var dx_max = MatrixSolvers.Error.Absolute(x0, x1);

            var convergenceTemp = true;

            if (TermalConvergence.Is_Switched_Tm && dx_max - TermalConvergence.Tm > 1e-4)
                convergenceTemp = false;

            return convergenceTemp;
        }

        public override bool CheckMaxResu(double x_max)
        {
            // Пока не контролируем
            return true;
        }

        public override double[] CalcResidualForces(double[] x1, NodesData geo, ElementsData<ElementTermal> mat, float timeStep)
        {
            CalcPhase(geo, mat, timeStep, x1);

            // заглушка
            return new double[1];
        }



        private void CalcPhase(NodesData geo, ElementsData<ElementTermal> elemsData, float timeStep, double[] x)
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

        

        public override MatrixContainer FormMatrices(NodesData nodes, IEnumerable<IElement> elements)
        {
            var matrixData = new MatrixContainer();
            List<int>[] incidence = nodes.GetGlobalNodesInc(elements, Dof);

            matrixData.AddMatrix(
                MatrixType.heatTransfer,
                BuildSymmetricMatrix(incidence));
            matrixData.AddMatrix(
                MatrixType.heatCapacity,
                BuildSymmetricMatrix(incidence));
            matrixData.AddMatrix(
                MatrixType.heatTransferCapacity,
                BuildSymmetricMatrix(incidence));

            return matrixData;
        }

        private static SymmetricCSRMatrix BuildSymmetricMatrix(List<int>[] incidence)
        {
            var builder = new SymmetricCSRMatrixBuilder(incidence.Length);
            for (int row = 0; row < incidence.Length; row++)
            {
                foreach (int col in incidence[row])
                {
                    if (col >= row)
                        builder.AddToElement(row, col, 1.0);
                }
            }

            SymmetricCSRMatrix matrix = builder.Build();
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

        public override void SaveTaskResults(VectorContainer<double> vec, ElementsData<ElementTermal> mat, NodesData geo, float time)
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
