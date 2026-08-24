using MaterialDB.MaterialData.MetallurgicalData;
using CAESolvers;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using Model;
using Model.Interfaces;
using Model.Interfaces.MeshObjects;
using Model.MeshObjects;
using Project.Interfaces;
using Project.Interfaces.Tasks;
using Project.TaskParameters;
using Project.Tasks;
using Project.Tasks.Functions.FrameFunctions;
using Project.Tasks.Materials;
using PropertiesCalculator.PropertiesController.Interfaces;
using PropertiesCalculator.PropertiesController.MechanicalModels;
using ResultDB;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Xml.Linq;
using TaskSolverCore.ElementData;
using TaskSolverCore.Matrix;
using TaskSolverCore.MatrixSolvers;
using TaskSolverCore.Utilities;
using TaskSolverCore.Vector;
using static IronPython.Modules._ast;
using static IronPython.Runtime.Profiler;
using static System.Net.WebRequestMethods;

namespace TaskSolverCore
{
    public abstract partial class MechTaskM : GeneralTask<ElementMechanical, SymmetricCSRMatrix>
    {
        public string ChemicalFile { get; set; } = "";
        public string ThermalFile { get; set; } = "";

        public bool UseThermalHistory { get; }

        public double ThermalLoad { get; }
        public MechanicalConvergence MechanicalConvergence { get; }   

        public IHardeningModel<float> HardeningModel { get; } = new ExponentialHardeningModel();

        internal List<ClampData> ClampData;
        internal List<LoadData> LoadData;

        public MechTaskM(int index, string folder,ITaskData taskData, MechanicalParameters parameters) 
            : base(index, folder, taskData, parameters, SolverBuilder.CreateSymmetric(parameters.SolverSettings))
        {
            Parameters = parameters;
            ClampData = taskData.Find<ClampData>().ToList();
            LoadData = taskData.Find<LoadData>().ToList();

            TaskKind = taskKind.механическая;

            ThermalFile = parameters.ThermalFile;
            UseThermalHistory = parameters.ThermalLoadIsFile;
            ThermalLoad = parameters.ThermalLoad;

            ChemicalFile = parameters.ChemicalFile;

            MechanicalConvergence = parameters.MechanicalConvergence;

            if (ThermalFile != "")
                TermalTimeSteps = LoadTermalTimes(ThermalFile);
        }

        public List<float> TermalTimeSteps { get; set; }
/// <inheritdoc/>

        public override ElementsData<ElementMechanical> CreateElementData()
        {
            var elementsNumbers = new HashSet<int>();
            var nodesNumbers = new List<int>();

            var elemsData = new ElementsData<ElementMechanical>();

            foreach (var dataItem in MatData)
            {
                var gr = dataItem.Group;

                var iniTemp = InitialTemp.Count > 0 ?
    InitialTemp[gr.Name] : 20.0;
                // пока проверять не будем,
                // так как если ключ таблицы не найдется,
                // будет читаемое исключение
                //dataItem.Material.CheckMechanicalProps();
                dataItem.Material.CheckPhaseData();

                var reactions = dataItem.Material["Металлургия"].PropertyData;
                var phaseTable = dataItem.Material["Общие сведения"]["Структура"].DataTable;

                var processData = new ProcessData(reactions.Values, MetallurgicalProcesses.ToArray());

                foreach (IElement obj in gr)
                {
                    if (elementsNumbers.Contains(obj.Number))
                        throw new Exception($"Элемент с номером {obj.Number} уже используется. Проверьте группы в разделе \"материалы\"");

                    elementsNumbers.Add(obj.Number);

                    foreach (var node in obj.GetVertexes())
                        nodesNumbers.Add(node.Number);

                    ElementMechanical eItem;

                    if (TaskType == TaskType.Volume | TaskType == TaskType.Volume_mixed)
                    {
                        if (obj.ObjType == ObjType.Элемент3D)
                        {
                            eItem = new EM3DV((IElement3D)obj);
                        }
                        else if (obj.ObjType == ObjType.Элемент2D)
                        {
                            var plateData = dataItem as PlateMatData;
                            eItem = new EM3DP(plateData.Thickness, (IElement2D)obj);
                        }
                        else
                        {
                            var beamData = dataItem as BeamMatData;
                            eItem = new EM3DB(beamData.Diameter, (Beam)obj);
                        }
                    }
                    else if (TaskType == TaskType.Plain)
                    {
                        eItem = new EM2DP(1, (IElement2D)obj);
                    }
                    else
                    {
                        if (obj.ObjType == ObjType.Элемент2D)
                            eItem = new EM2DAV((IElement2D)obj);
                        else
                        {
                            var plateData = dataItem as PlateMatData;
                            eItem = new EM2DAP(plateData.Thickness, (Beam)obj);
                        }
                    }


                    eItem.Temp = (float)iniTemp;
                    eItem.FusionTemp = Convert.ToSingle(
                        dataItem.Material["Общие сведения"]["Кристаллизация"].
                        DataTable.Rows[0]["Температура"]);
                    eItem.Material = dataItem.Material.Name;
                    eItem.ProcessData = processData;
                    eItem.PhaseData = new PhaseData(phaseTable);


                    elemsData.Add(eItem);
                }

                //var _elementsNumbers = elementsNumbers.ToList();
                //_elementsNumbers.Sort();
                //nodesNumbers = nodesNumbers.Distinct().ToList();
                //nodesNumbers.Sort();
            }
            return elemsData;
        }

        /// <inheritdoc/>
        public override Result CreateIntialResult(float time, ElementsData<ElementMechanical> elementsData)
        {
            // все фазы какие только есть во всех материалах
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

            foreach (var item in elementsData)
            {
                var phases = item.PhaseData;

                var eleRow = elemTable.NewRow();
                eleRow["Индекс"] = item.Number;
                eleRow["T"] = item.Temp;

                eleRow["St"] = item.Yield;

                var heatStrain = item.TermalStrain_Calc();

                eleRow["Et"] = heatStrain[0];

                foreach (var phase in phases)
                    eleRow[phase.Name] = phase.Value;

                elemTable.Rows.Add(eleRow);

                foreach (var nodes in item.Element.GetVertexes())
                {
                    var nodRow = nodesTable.Rows.Find(nodes.Number);
                    nodRow["Индекс"] = nodes.Number;
                    nodRow["T"] = item.Temp;

                    foreach (var phase in phases)
                        nodRow[phase.Name] = phase.Value;

                }
                //}
            }
            return new Result(dataSet, time, TaskKind.ToString());
        }

        public abstract Vector<double> GetDisplacements(List<int> inds, double[] x);      

        public override void CheckLoadAndBoundaryConditions(NodeDofMap geo, ElementsData<ElementMechanical> elementsData)
        {
            var nodeLoadData = LoadData.Where(x => x.Group.ObjType == ObjType.Узел);
            CheckLoadData(geo, elementsData,nodeLoadData);

            var nodeClampData = ClampData.Where(x => x.Group.ObjType == ObjType.Узел);
            CheckLoadData(geo, elementsData,nodeClampData);
        }

        public override void SaveTaskResults(VectorContainer<double> vec, ElementsData<ElementMechanical> mat,NodeDofMap geo,float time)
        {
            var x = vec.GetVectorArray(VectorType.result);
            var y = vec.GetVectorArray(VectorType.force);
            var r = vec.GetVectorArray(VectorType.reaction);

            var dataSet = new DataSet();

            var nTable = taskResults.Last().Data.Tables["nodes"].Clone();
            SaveNodesResults(geo, r, x.Vector,nTable);
            dataSet.Tables.Add(nTable);
            var eTable = taskResults.Last().Data.Tables["elements"].Clone();
            SaveElemensResults(mat, geo, time, x.Vector, eTable);
            dataSet.Tables.Add(eTable);
            taskResults.Add(new Result(dataSet,time,TaskKind.ToString()));
        }

        private void SaveElemensResults(ElementsData<ElementMechanical> elemsData, NodeDofMap geo, float time, double[] x, DataTable dataTable)
        {
            foreach (var elemData in elemsData)
            {
                var tempS = (float)taskResults.Last().Data.Tables["elements"].Rows.Find(elemData.Number)["T"];

                var d_strainT = elemData.IncTermalStrain_Calc(elemData.Temp - tempS);

                var inds = geo.GetGlobalInds(elemData.Element, Dof);

                var d_displs = GetDisplacements(inds, x);

                var d_strain = elemData.Strain_Calc(d_displs);

                d_strain.Subtract(d_strainT, d_strain);

                var strain = GetElasticStrain(elemData.Number);
                strain.Add(d_strain, strain);

                var stress = elemData.Stress_Calc(strain);
                var strainE = elemData.ElasticStrain_Calc(stress);
                var strainP = strain - strainE;


                var misE = elemData.IntensityStrain_Calc(strain);
                //var misEe = PlasticityModel.IntensityStrain_Calc(strainE);
                var misEp = elemData.IntensityStrain_Calc(strainP);

                var time0 = taskResults.Last().Time;
                var dtime = time - time0;

                //var strainC = mat[i].CreepModel.CreepStrain_Calc(dtime, misEp, mat[i].Relax, mat[i].Young, strainE);
                //strainE = strainE.Subtract(strainC);

                var misS = elemData.IntensityStress_Calc(stress);

                var workRow = dataTable.NewRow();

                workRow["Индекс"] = elemData.Element.Number;
                workRow["T"] = elemData.Temp;
                workRow["Et"] = d_strainT[0] + (float)taskResults.Last().Data.Tables["elements"].Rows.Find(elemData.Number)["Et"];

                if (elemData.Status == 1)
                {
                    workRow["Smis"] = misS;
                    workRow["Emis"] = misE;

                    var cumEp = (float)taskResults.Last().Data.Tables["elements"].Rows.Find(elemData.Number)["Ep"] + misEp;

                    if (Math.Abs(elemData.Temp - elemData.FusionTemp) < 1e-4) 
                        cumEp = 0;
                    workRow["Ep"] = cumEp;

                    workRow["St"] = elemData.Yield;
                    workRow["Smean"] = (stress[0] + stress[1] + stress[2]) / 3;

                    SaveElemResultsTensor(strain, stress, strainE, workRow);

                    foreach (var phase in elemData.PhaseData)
                        workRow[phase.Name] = phase.Value;
                }

                dataTable.Rows.Add(workRow);
            }
        }

        public abstract void SaveElemResultsTensor(Vector<double> strain, Vector<double> stress, Vector<double> strainE, DataRow workRow);

        public abstract void SaveNodesResults(NodeDofMap geo, VectorArray<double> r, double[] dist, DataTable dataTable);
        

        protected override void SetPhysicalProperties(
            TaskSystemContext<ElementMechanical> context)
        {
            var geo = context.Nodes;
            var elemsData = context.Elements;
            var time = context.Time;
            var resultLast = taskResults.Last();

            Result? termalResult;

            var flag = TryGetTermalResult(time, out termalResult);

            foreach (var data in MatData)
            {
                var gr = data.Group;

                foreach (var obj in gr)
                {
                    var elemData = elemsData[obj.Number];


                    
                    var temp0 = (float)resultLast.Data.Tables["elements"].Rows.Find(obj.Number)["T"];
                    if (flag)
                    {
                        var time1 = termalResult.Time;
                        var time0 = resultLast.Time;

                        var temp1 = (float)termalResult.Data.Tables["elements"].Rows.Find(obj.Number)["T"];
                        
                        elemData.Temp = Search.InterpolatedValueTwoPoints(time0, time1, temp0, temp1, time);
                        
                        GetPhases(termalResult, elemData);
                    }
                    else
                        elemData.Temp = (float)ThermalLoad;


                    if (time >= data.StartTime & time <= data.StopTime)
                    {
                        GetProperties(data, elemData);
                    }

                    else
                    {
                        elemData.Young = 1000;
                        elemData.Status = 0;
                    }
                    // попробуем отсекать тепловые деформации если превышена 
                    // температура плавления

                    if (elemData.Temp > elemData.FusionTemp)
                        elemData.Temp = elemData.FusionTemp;

                    var G = elemData.Young / (2 * (1 + 0.3f));
                    elemData.Phi = 1 / (2 * G); // итерация - элементmaterialData.phi[ind]}     
                                                 // сheck melting point
                    var cum_misEp = (float)resultLast.Data.Tables["elements"].Rows.Find(obj.Number)["Ep"];

                    var hardening = HardeningModel.Calc(elemData.Yield, elemData.Slope, elemData.Tensile, cum_misEp);
                    elemData.Yield += hardening;
                }

            }
        }

        private static void GetProperties(MatData data, ElementMechanical elemData)
        {
            var phaseData = elemData.PhaseData;
            elemData.Yield = data.Material["Механические свойства"]["Предел текучести"].CalcProp(phaseData, elemData.Temp);
            elemData.Tensile = data.Material["Механические свойства"]["Предел прочности"].CalcProp(phaseData, elemData.Temp);
            elemData.Slope = data.Material["Механические свойства"]["Коэффициент упрочнения"].CalcProp(phaseData, elemData.Temp);

            if (Convert.ToInt32(data.Material["Общие сведения"]["Модель материала"].DataTable.Rows[0]["Модель материала"]) == 2)
            {
                var young_x = data.Material["Механические свойства"]["Модуль Юнга X"].CalcProp(phaseData, elemData.Temp);
                var young_y = data.Material["Механические свойства"]["Модуль Юнга Y"].CalcProp(phaseData, elemData.Temp);
                var young_z = data.Material["Механические свойства"]["Модуль Юнга Z"].CalcProp(phaseData, elemData.Temp);

                // пока временное усреднение свойств
                elemData.Young = (young_x + young_y + young_z) / 3;

                elemData.HeatExpCoeff[0] = data.Material["Механические свойства"]["ТКЛР X"].CalcProp(phaseData, elemData.Temp);
                elemData.HeatExpCoeff[1] = data.Material["Механические свойства"]["ТКЛР Y"].CalcProp(phaseData, elemData.Temp);
                elemData.HeatExpCoeff[2] = data.Material["Механические свойства"]["ТКЛР Z"].CalcProp(phaseData, elemData.Temp);
            }
            else
            {
                elemData.Young = data.Material["Механические свойства"]["Модуль Юнга"].CalcProp(phaseData, elemData.Temp);

                var hexp = data.Material["Механические свойства"]["ТКЛР"].CalcProp(phaseData, elemData.Temp);

                elemData.HeatExpCoeff[0] = hexp;
                elemData.HeatExpCoeff[1] = hexp;
                elemData.HeatExpCoeff[2] = hexp;
            }

            elemData.Status = 1;
        }

        private bool TryGetTermalResult(float time, out Result? result)
        {
            if (UseThermalHistory && ThermalFile != "")
            {
                var termalTime = TermalTimeSteps.Find(x => x.CompareTo(time) >= 0);

                var termalDB = Directory.GetFiles($@"{Folder}\ResultsData", ThermalFile);
                if (termalDB.Length == 0)
                    throw new Exception($"Не найден файл {ThermalFile}");

                result = ResultsLoader.GetResult(termalDB[0], new List<string>() { "elements" }, termalTime);
                return true;
            }

            result = null;
            return false;
        }

        private void GetPhases(Result termal, ElementItem element)
        { 
            foreach (var item in element.PhaseData)
            {
                var col = termal.Data.Tables["elements"].Columns[item.Name];
                if (col != null)
                    item.Value = (float)termal.Data.Tables["elements"].Rows.Find(element.Number)[item.Name];
                else
                    throw new Exception(@$"фаза ""{item.Name}"" не найдена в базе результатов");
                //for (int i = 4; i < termal.Data.Tables["elements"].Columns[].Count; i++)
                //{
                //    if(termal.Data.Tables["elements"].Columns[i].ColumnName == item.Name)
                        
                //}
            }
        }

        public void Bound_mK(int index, ref float[] y, float disp, ref float[][] mK)
        {
            // Отображение граничных условий на матрицу жесткости 
            // преобразование внедиагональных элементов и вектора нагрузки
            for (int k = 0; k < mK[index].Length; k++)
            {
                if (k != index)
                {
                    mK[index][k] = 0;
                    y[k] = y[k] - (mK[k][index] * disp);
                    mK[k][index] = 0;
                }
            }
            // преобразование вектора нагрузки
            y[index] = mK[index][index] * disp;
        }


        //public override Tuple<bool, double, float> CheckConvergence(double[] x1, double[] x0, NodeDofMap geo, ElementsData<ElementMechanical> mat, float timeStep)
        //{
        //    var dx = 0.0;
        //    var dy = new double[mat.Count];

        //    var convergence = true;

        //    var checkDist = CheckDistortions(x1, x0, ref dx);
        //    var checkStr = CheckStresses(x1, dy, geo, mat);

        //    if (checkDist & checkStr)
        //        convergence = true;
        //    else convergence = false;

        //    return new Tuple<bool, double, float>(convergence, dx, (float)dy.Max());
        //}

        /// <inheritdoc/>
        private double[] CalculatePhysicalResidual(
            double[] x,
            NodeDofMap geo,
            ElementsData<ElementMechanical> mat,
            bool matrixUpdateScheduled)
        {
            //bool checkConverg = true;
            var dy = new double[mat.Count];
            if (MechanicalConvergence.Is_Physically_NonLinear)
            {
                    var counter = 0;
                foreach (var item in mat)
                {
                    var tempS = (float)taskResults.Last().Data.Tables["elements"].Rows.Find(item.Number)["T"];                   

                    var d_strainT = item.IncTermalStrain_Calc(item.Temp - tempS);//strainTE.Subtract(strainTS);

                    var inds = geo.GetGlobalInds(item.Element, Dof);

                    var d_displs = GetDisplacements(inds, x);
                    var d_strain = item.Strain_Calc(d_displs);

                    d_strain.Subtract(d_strainT, d_strain);
                    d_strain.Subtract(item.Tensor, d_strain);

                    if (item.Status == 0)
                        d_strain.Clear();

                    var strain = GetElasticStrain(item.Number);

                    strain = strain.Add(d_strain);

                    var stress = item.Stress_Calc(strain);

                    var misE = item.IntensityStrain_Calc(strain);
                    var misS = item.IntensityStress_Calc(stress);

                    var hardening = HardeningModel.Calc(item.Yield, item.Slope, item.Tensile, (float)misE);

                    var yield_i = item.Yield + hardening;

                    dy[counter] = misS / yield_i;
                    //chech yield
                    if (dy[counter] - MechanicalConvergence.SiStm > 1e-4)
                    {
                        if (matrixUpdateScheduled)
                        {
                            item.Phi = (float)Math.Pow(dy[counter], MechanicalConvergence.MaterialPlasticityCoeff) * item.Phi;
                            item.Tensor.Clear();
                        }
                        else
                        {
                            var dev_s = item.Deviator_Calc(stress);

                            var k = yield_i / misS;
                            dev_s.Multiply(k, dev_s);

                            var mean = item.Ball_Calc(stress);
                            var real = mean.Add(dev_s);
                            var cor = stress.Subtract(real);
                            var delta = item.ElasticStrain_Calc(cor);
                            item.Tensor = item.Tensor.Add(delta);
                        }
                        //checkConverg = false;
                    }
                    counter++;
                }

                //if (dy.Max() - MechanicalConvergence.SiStm > 1e-4)
                //    checkConverg = false;

            }

            //return new Tuple<bool, double>(checkConverg, dy.Max());
            return dy;
        }

        protected override TaskIterationResult EvaluateIteration(
            TaskSystemContext<ElementMechanical> context,
            double[] solution,
            double solutionChange,
            double solutionMaximum,
            bool matrixUpdateScheduled)
        {
            var converged =
                solutionChange - MechanicalConvergence.DUm <= 1e-4;
            var canContinue =
                !MechanicalConvergence.Is_Switched_Um ||
                solutionMaximum - MechanicalConvergence.Um <= 1e-4;
            var residuals = CalculatePhysicalResidual(
                solution,
                context.Nodes,
                context.Elements,
                matrixUpdateScheduled);
            var physicalResidual = residuals.Max();

            if (physicalResidual > 30.0)
                canContinue = false;

            return new TaskIterationResult(
                converged,
                canContinue,
                matrixUpdateScheduled,
                solutionChange,
                solutionMaximum,
                physicalResidual);
        }

        protected override void ApplyBoundaryConditions(
            TaskSystemContext<ElementMechanical> context)
        {
            var vec = context.Vectors;
            var matr = context.Matrices;
            var geo = context.Nodes;
            var time = context.Time;
            var y = vec.GetVectorArray(VectorType.force);

            SymmetricCSRMatrix mK =
                matr.Get<SymmetricCSRMatrix>(MatrixType.stifness);
            //var trNumInds = mK.TransposeIndexes();

            foreach (var data in ClampData)
            {
                if (time >= data.StartTime & time <= data.StopTime)
                {
                    var dataSet = taskResults.Last().Data;
                    var group = data.Group;

                    foreach (var obj in group)
                    {
                        var dirInd = 0;
                        var objInd = geo.IndexOfNode(obj.Number);

                        if (data.Direction == Direction.X) dirInd = 0;
                        else if (data.Direction == Direction.Y) dirInd = 1;
                        else dirInd = 2;

                        var sInd = (Dof * objInd) + dirInd;

                        if (data.ClampKind == ClampKind.Жесткое)
                        {
                            mK.LineCross(y.Vector, data.Value, sInd);
                        }
                        //else if (data.ClampKind == ClampKind.Контакт)
                        //{
                        //    var disp = (float)dataSet.Tables["nodes"].Rows[objInd][sInd];
                        //    var stiffFunc = data.TimeFunction.CalcProp(disp);

                        //    var penForce = -stiffFunc * disp;
                        //    y[sInd] = y[sInd] + penForce;
                        //}

                    }
                }
            }
            //var str = mK.ToString();
        }

        protected override void ApplyLoads(
            TaskSystemContext<ElementMechanical> context)
        {
            var vec = context.Vectors;
            var matr = context.Matrices;
            var geo = context.Nodes;
            var time = context.Time;
            var y = vec.GetVectorArray(VectorType.force);
            SymmetricCSRMatrix mK =
                matr.Get<SymmetricCSRMatrix>(MatrixType.stifness);
            //var transInds = mK.TransposeIndexes();

            foreach (var data in LoadData)
            {
                if (time >= data.StartTime & time <= data.StopTime)
                {
                    var refTime = time - data.StartTime;

                    var taskInd = Dof;
                    var group = data.Group;
                    var val = data.Value;

                    if (data.Function != null && data.Function.ContainsParameter("TIME"))
                    {
                        data.Function["TIME"].SetValue(refTime);
                        val = val * data.Function.CalcValue();
                    }



                    foreach (var obj in group)
                    {
                        var dirInd = 0;
                        var nodeInd = geo.IndexOfNode(obj.Number);

                        if (data.Direction == Direction.X) dirInd = 0;
                        else if (data.Direction == Direction.Y) dirInd = 1;
                        else dirInd = 2;

                        var sind = (taskInd * nodeInd) + dirInd;

                        if (data.LoadKind == LoadKind.Давление)
                            mK.LineCross(y.Vector, val, nodeInd);
                        else if (data.LoadKind == LoadKind.Сила)
                            y[sind] = y[sind] + val;

                    }
                }
            }
        }




        protected override void ApplyPreLoads(
            TaskSystemContext<ElementMechanical> context)
        {          
            var vec = context.Vectors;
            var geo = context.Nodes;
            var mat = context.Elements;
            var y = vec.GetVectorArray(VectorType.force);
            var p = vec.GetVectorArray(VectorType.plastic);
            var r = vec.GetVectorArray(VectorType.reaction);

            var t = new VectorArray<double>(y.Length);
            var d = new VectorArray<double>(y.Length);
            //if (iter == 0)
            //    SetIniDisplacements(geo, iter, x);

            //var dataSet = taskResu.Last();

            foreach (var item in mat)
            {
                //var obj = mat[i].Element;
                var tempS = (float)taskResults.Last().Data.Tables["elements"].Rows.Find(item.Number)["T"];

                var d_strainT = item.IncTermalStrain_Calc(item.Temp - tempS);

                var strainE = GetElasticStrain(item.Number);

                var inds = geo.GetGlobalInds(item.Element,Dof);

                var forceE = item.Force_Calc(strainE);
                SummForce_Calc(inds, r, forceE);
                var d_forceT = item.Force_Calc(d_strainT);
                SummForce_Calc(inds, t, d_forceT);
     
                var forceD = item.Force_Calc(item.Tensor);
                SummForce_Calc(inds, d, forceD);
            }
            // вычисление сил от пластических деформаций с учетом релаксации
            var w = 0.0001;
            d.Multiply(w);
            p.Multiply(1 - w);
            var cur = d.Sum(p.Vector);

            vec.RemoveVectors(VectorType.plastic);
            vec.AddVector(VectorType.plastic, cur);

            r.Multiply(-1);

            // добавка для сил от начальных упругих деформаций
            y.Sum(r.Vector,y);

            // добавка для сил от теплового расширения
            y.Sum(t.Vector,y);

            // добавка для итоговых сил от пластических деформаций
            y.Sum(cur.Vector,y);
        }

        //public abstract VectorArray<double> GetIniDisplacements(NodeDofMap geo, int iter, VectorList<double> x);

        public abstract void SummForce_Calc(List<int> inds, VectorArray<double> nLoads, Vector<double> eLoads);

        public abstract Vector<double> GetElasticStrain(int eNumber);
 /// <inheritdoc/>
 

        public override MatrixContainer FormMatrices(NodeDofMap nodes, IEnumerable<IElement> elements)
        {
            var matrixData = new MatrixContainer();
            matrixData.AddMatrix(
                MatrixType.stifness,
                BuildSymmetricMatrix(nodes, elements, Dof));

            return matrixData;
        }

        protected override LinearSystem<SymmetricCSRMatrix> CreateLinearSystem(TaskSystemContext<ElementMechanical> context)
        {
            var matrix = context.Matrices.Get<SymmetricCSRMatrix>(
                MatrixType.stifness);
            var rightHandSide = context.Vectors
                .GetVectorArray(VectorType.force)
                .Vector
                .ToArray();

            return new LinearSystem<SymmetricCSRMatrix>(matrix, rightHandSide);
        }

        private static SymmetricCSRMatrix BuildSymmetricMatrix(
            NodeDofMap nodes,
            IEnumerable<IElement> elements,
            int degreesOfFreedom)
        {
            var builder = new SymmetricCSRMatrixBuilder(
                nodes.Count * degreesOfFreedom);

            foreach (var element in elements)
            {
                var indices = nodes.GetGlobalInds(
                    element, degreesOfFreedom);

                for (var localRow = 0;
                    localRow < indices.Count;
                    localRow++)
                {
                    for (var localColumn = localRow;
                        localColumn < indices.Count;
                        localColumn++)
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

        public override VectorContainer<double> FormVectors(int nmbrNodes, int numbrEls)
        {
            var vectorData = new VectorContainer<double>();

            if (TaskType == TaskType.Volume | TaskType == TaskType.Volume_mixed)
            {
                vectorData.AddVector(VectorType.force, nmbrNodes * 3);
                vectorData.AddVector(VectorType.reaction, nmbrNodes * 3);
                vectorData.AddVector(VectorType.result, nmbrNodes * 3);
                vectorData.AddVector(VectorType.plastic, nmbrNodes * 3);
            }
            else
            {
                vectorData.AddVector(VectorType.force, nmbrNodes * 2);
                vectorData.AddVector(VectorType.reaction, nmbrNodes * 2);
                vectorData.AddVector(VectorType.result, nmbrNodes * 2);
                vectorData.AddVector(VectorType.plastic, nmbrNodes * 2);
            }
            return vectorData;
        }

        public override void ClearMatrices(MatrixContainer matrixData)
        {
            matrixData.ClearMatrixes();
        }

        public override void ClearVectorLoads(VectorContainer<double> vec)
        {
            vec.GetVectorArray(VectorType.force).Clear();
            vec.GetVectorArray(VectorType.reaction).Clear();
        }

        public override void ClearVectorResults(VectorContainer<double> vec)
        {
            vec.GetVectorArray(VectorType.result).Clear();
        }
        /// <summary>
        /// LoadTermalTimes
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public List<float> LoadTermalTimes(string termalFile)
        {
            //var file = Path.GetFileName(ThermalFile);

            var dbTermNames = Directory.GetFiles($@"{Folder}\ResultsData", termalFile);
            if (dbTermNames.Count() != 0)
                return ResultsLoader.GetValues(dbTermNames[0], "elements","Time").ToList();
            else
                throw new Exception($"Не найден файл {termalFile}...");
        }

        protected override void FillMatrices(
            TaskSystemContext<ElementMechanical> context)
        {
            var matr = context.Matrices;
            var elemData = context.Elements;
            var geo = context.Nodes;
            SymmetricCSRMatrix mKC =
                matr.Get<SymmetricCSRMatrix>(MatrixType.stifness);

            //var length = elemData.Count();

            foreach (var item in elemData)
            {
                //var eObj = elemData[i].Element;
                var mLocStiff = item.Stiffness_Calc();

                //var ndInds = mInd[i].Count;
                var gInds = geo.GetGlobalInds(item.Element, Dof);

                for (int k = 0; k < gInds.Count; k++)
                {
                    var row = gInds[k];

                    for (int m = 0; m < gInds.Count; m++)
                    {
                        var col = gInds[m];

                        if (col >= row)
                            mKC.AccumulateAt(row, col, mLocStiff[k, m]);
                    }
                }
                //}
            }

        }
    }
}
