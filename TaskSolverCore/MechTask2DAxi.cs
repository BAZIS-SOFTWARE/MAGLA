using MathNet.Numerics.LinearAlgebra;
using Project.Interfaces;
using Project.Interfaces.Tasks;
using Project.TaskParameters;
using ResultDB;
using System.Data;
using TaskSolverCore.Matrix;
using TaskSolverCore.Vector;

namespace TaskSolverCore
{
    public class MechTask2DAxi : MechTaskM
    {
        public MechTask2DAxi(int index, string folder,ITaskData taskData, MechanicalParameters parameters) 
            : base(index, folder, taskData, parameters)
        {
            Dof = 2;
                //PhysMatrixCalculator = new Mech2DAxiCalculator();
        }

        public override DataSet CreateDataSet(List<string> phasesNames)
        {
            var dataSet = new DataSet();

            var dicNodes = new Dictionary<string, Type>()
            {
                { "Индекс", typeof(int) },
                { "X", typeof(float) },
                { "Y", typeof(float) },
                { "XY", typeof(float)},
                { "Rx", typeof(float)},
                { "Ry", typeof(float)},
                { "Rxy", typeof(float)},
                { "T", typeof(float)},
                { "Ex", typeof(float)},
                { "Ey", typeof(float)},
                { "Ez", typeof(float)},
                { "Exy", typeof(float)},
                { "Eex", typeof(float)},
                { "Eey", typeof(float)},
                { "Eez", typeof(float)},
                { "Eexy", typeof(float)},
                { "Emis", typeof(float)},
                { "Emean", typeof(float)},
                { "Et", typeof(float)},
                { "Ep", typeof(float)},
                { "Sx", typeof(float)},
                { "Sy", typeof(float)},
                { "Sz", typeof(float)},
                { "Sxy", typeof(float)},
                { "Smis", typeof(float)},
                { "Smean", typeof(float)},
                { "St", typeof(float)}
            };

            phasesNames.ForEach(x => dicNodes.Add(x, typeof(float)));

            var nTable = dataSet.Tables.Add("nodes");

            foreach (var column in dicNodes)
            {
                var newColumn = new DataColumn(column.Key, column.Value)
                { DefaultValue = 0 };
                nTable.Columns.Add(newColumn);
            }

            var keyN = new DataColumn[1];
            keyN[0] = nTable.Columns[0];
            nTable.PrimaryKey = keyN;

            var dicElems = new Dictionary<string, Type>()
            {
                { "Индекс", typeof(int) },
                { "T", typeof(float)},
                { "Ex", typeof(float)},
                { "Ey", typeof(float)},
                { "Ez", typeof(float)},
                { "Exy", typeof(float)},
                { "Eex", typeof(float)},
                { "Eey", typeof(float)},
                { "Eez", typeof(float)},
                { "Eexy", typeof(float)},
                { "Emis", typeof(float)},
                { "Emean", typeof(float)},
                { "Et", typeof(float)},
                { "Ep", typeof(float)},
                { "Sx", typeof(float)},
                { "Sy", typeof(float)},
                { "Sz", typeof(float)},
                { "Sxy", typeof(float)},
                { "Smis", typeof(float)},
                { "Smean", typeof(float)},
                { "St", typeof(float)}
            };

            phasesNames.ForEach(x => dicElems.Add(x, typeof(float)));

            var eTable = dataSet.Tables.Add("elements");

            foreach (var column in dicElems)
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
/// <inheritdoc/>

        public override Vector<double> GetElasticStrain(int eNumber)
        {
            var dataRow = taskResults.Last().Data.Tables["elements"].Rows.Find(eNumber);

            var ex = (float)dataRow["Eex"];
            var ey = (float)dataRow["Eey"];
            var ez = (float)dataRow["Eez"];
            var exy = (float)dataRow["Eexy"];

            return Vector<double>.Build.Dense(new double[] { ex, ey, ez, exy});
        }

        public override void SaveElemResultsTensor(Vector<double> strain, Vector<double> stress, Vector<double> strainE, DataRow workRow)
        {
            workRow["Sx"] = stress[0];
            workRow["Sy"] = stress[1];
            workRow["Sz"] = stress[2];
            workRow["Sxy"] = stress[3];
            workRow["Ex"] = strain[0];
            workRow["Ey"] = strain[1];
            workRow["Ez"] = strain[1];
            workRow["Exy"] = strain[3];
            workRow["Eex"] = strainE[0];
            workRow["Eey"] = strainE[1];
            workRow["Eez"] = strainE[2];
            workRow["Eexy"] = strainE[3];
        }

        public override void SaveNodesResults(NodesData geo, VectorArray<double> r, double[] dist,DataTable dataTable)
        {
            var nodesCount = geo.Count;
            var nodes = geo.GetNodesNumbs.ToList();

            for (int i = 0; i < nodesCount; i++)
            {
                var workRow = dataTable.NewRow();
                workRow["Индекс"] = nodes[i];

                // получаем индекс узла по номеру. Учитывая перенумерацию.
                var ind = geo.IndexOfNode(nodes[i]);

                var indX = (2 * ind) + 0;
                var indY = (2 * ind) + 1;

                var x = dist[indX] + (float)taskResults.Last().Data.Tables["nodes"].Rows[i]["X"];
                var y = dist[indY] + (float)taskResults.Last().Data.Tables["nodes"].Rows[i]["Y"];

                workRow["X"] = x;
                workRow["Y"] = y;
                workRow["XY"] = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
                workRow["Rx"] = r[indX];
                workRow["Ry"] = r[indY];
                workRow["Rxy"] = Math.Sqrt(Math.Pow(r[indX], 2) + Math.Pow(r[indY], 2));

                dataTable.Rows.Add(workRow);
            }
        }

        public override Vector<double> GetDisplacements(List<int> nInds,  double[] x)
        {
            var displeNode = Vector<double>.Build.Dense(nInds.Count);

            for (int j = 0; j < nInds.Count / 2; j++)
            {
                int indX, indY;

                indX = nInds[(2 * j) + 0];
                indY = nInds[(2 * j) + 1];

                displeNode[(2 * j) + 0] = x[indX];
                displeNode[(2 * j) + 1] = x[indY];
            }

            return displeNode;
        }

        public override void SummForce_Calc(List<int> inds, VectorArray<double> nLoads, Vector<double> eLoads)
        {
            var vNumbs = inds.Count;

            var nNumbs = vNumbs / 2;
            for (int j = 0; j < nNumbs; j++)
            {
                var indX = inds[(2 * j) + 0];
                var indY = inds[(2 * j) + 1];

                nLoads[indX] = nLoads[indX] + eLoads[(2 * j) + 0];
                nLoads[indY] = nLoads[indY] + eLoads[(2 * j) + 1];
            }
        }

        //public override VectorArray<double> GetIniDisplacements(NodesData geo, int iter, VectorList<double> x)
        //{
        //    return new VectorArray<double>(geo.Count * 2);
        //}
    }
}
