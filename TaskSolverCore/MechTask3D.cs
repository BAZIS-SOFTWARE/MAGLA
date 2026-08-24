using MathNet.Numerics.LinearAlgebra;
using Project.Interfaces;
using Project.Interfaces.Tasks;
using Project.TaskParameters;
using ResultDB;
using System.Data;
using TaskSolverCore.Matrix;
using TaskSolverCore.Vector;
using static IronPython.Runtime.Profiler;

namespace TaskSolverCore
{
    public class MechTask3D : MechTaskM
    {
        public MechTask3D(int index, string folder,ITaskData taskData, MechanicalParameters parameters) : base(index, folder,taskData, parameters)
        {
            Dof = 3;
            //PhysMatrixCalculator = new Mech3DCalculator();
        }

        public override DataSet CreateDataSet(List<string> phasesNames)
        {
            var dataSet = new DataSet();

            var dicNodes = new Dictionary<string, Type>()
            {
                { "Индекс", typeof(int) },
                { "X", typeof(float) },
                { "Y", typeof(float) },
                { "Z", typeof(float)},
                { "XYZ", typeof(float)},
                { "Rx", typeof(float)},
                { "Ry", typeof(float)},
                { "Rz", typeof(float)},
                { "Rxyz", typeof(float)},
                { "T", typeof(float)},
                { "Ex", typeof(float)},
                { "Ey", typeof(float)},
                { "Ez", typeof(float)},
                { "Exy", typeof(float)},
                { "Exz", typeof(float)},
                { "Eyz", typeof(float)},
                { "Eex", typeof(float)},
                { "Eey", typeof(float)},
                { "Eez", typeof(float)},
                { "Eexy", typeof(float)},
                { "Eexz", typeof(float)},
                { "Eeyz", typeof(float)},
                { "Emis", typeof(float)},
                { "Emean", typeof(float)},
                { "Et", typeof(float)},
                { "Ep", typeof(float)},
                { "Sx", typeof(float)},
                { "Sy", typeof(float)},
                { "Sz", typeof(float)},
                { "Sxy", typeof(float)},
                { "Sxz", typeof(float)},
                { "Syz", typeof(float)},
                { "Smis", typeof(float)},
                { "Smean", typeof(float)},
                { "St", typeof(float)}
            };

            var nTable = dataSet.Tables.Add("nodes");

            phasesNames.ForEach(x => dicNodes.Add(x, typeof(float)));

            foreach (var column in dicNodes)
            {
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                var newColumn = new DataColumn(column.Key, column.Value)
                { DefaultValue = 0 };
#pragma warning restore IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
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
                { "Exz", typeof(float)},
                { "Eyz", typeof(float)},
                { "Eex", typeof(float)},
                { "Eey", typeof(float)},
                { "Eez", typeof(float)},
                { "Eexy", typeof(float)},
                { "Eexz", typeof(float)},
                { "Eeyz", typeof(float)},
                { "Emis", typeof(float)},
                { "Emean", typeof(float)},
                { "Et", typeof(float)},
                { "Ep", typeof(float)},
                { "Sx", typeof(float)},
                { "Sy", typeof(float)},
                { "Sz", typeof(float)},
                { "Sxy", typeof(float)},
                { "Sxz", typeof(float)},
                { "Syz", typeof(float)},
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

        public override Vector<double> GetElasticStrain(int eNumber)
        {
            var dataRow = taskResults.Last().Data.Tables["elements"].Rows.Find(eNumber);

            var ex = (float)dataRow["Eex"];
            var ey = (float)dataRow["Eey"];
            var ez = (float)dataRow["Eez"];
            var exy = (float)dataRow["Eexy"];
            var exz = (float)dataRow["Eexz"];
            var eyz = (float)dataRow["Eeyz"];

            return Vector<double>.Build.Dense(new double[] { ex, ey, ez, exy, exz, eyz });
        }

        public override void SaveElemResultsTensor(Vector<double> strain, Vector<double> stress, Vector<double> strainE, DataRow workRow)
        {
            workRow["Sx"] = stress[0];
            workRow["Sy"] = stress[1];
            workRow["Sz"] = stress[2];
            workRow["Sxy"] = stress[3];
            workRow["Sxz"] = stress[4];
            workRow["Syz"] = stress[5];
            workRow["Ex"] = strain[0];
            workRow["Ey"] = strain[1];
            workRow["Ez"] = strain[2];
            workRow["Exy"] = strain[3];
            workRow["Exz"] = strain[4];
            workRow["Eyz"] = strain[5];
            workRow["Eex"] = strainE[0];
            workRow["Eey"] = strainE[1];
            workRow["Eez"] = strainE[2];
            workRow["Eexy"] = strainE[3];
            workRow["Eexz"] = strainE[4];
            workRow["Eeyz"] = strainE[5];
        }

        public override void SaveNodesResults(NodeDofMap geo, VectorArray<double> r, double[] dist, DataTable dataTable)
        {
            var nodesCount = geo.Count;
            var nodes = geo.GetNodesNumbs.ToList();

            for (int i = 0; i < nodesCount; i++)
            {
                var workRow = dataTable.NewRow();
                workRow["Индекс"] = nodes[i];

                // получаем индекс узла по номеру. Учитывая перенумерацию.
                var ind = geo.IndexOfNode(nodes[i]);

                var indX = (3 * ind) + 0;
                var indY = (3 * ind) + 1;
                var indZ = (3 * ind) + 2;

                var x = dist[indX] + (float)taskResults.Last().Data.Tables["nodes"].Rows[i]["X"];
                var y = dist[indY] + (float)taskResults.Last().Data.Tables["nodes"].Rows[i]["Y"];
                var z = dist[indZ] + (float)taskResults.Last().Data.Tables["nodes"].Rows[i]["Z"];

                workRow["X"] = x;
                workRow["Y"] = y;
                workRow["Z"] = z;
                workRow["XYZ"] = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2) + Math.Pow(z, 2));
                workRow["Rx"] = r[indX];
                workRow["Ry"] = r[indY];
                workRow["Rz"] = r[indZ];
                workRow["Rxyz"] = Math.Sqrt(Math.Pow(r[indX], 2) + Math.Pow(r[indY], 2) + Math.Pow(r[indZ], 2));

                dataTable.Rows.Add(workRow);
            }
        }

        public override Vector<double> GetDisplacements(List<int> inds, double[] x)
        {
            var displeNode = Vector<double>.Build.Dense(inds.Count);

            for (int j = 0; j < inds.Count / 3; j++)
            {
                int indX, indY, indZ;

                indX = inds[(3 * j) + 0];
                indY = inds[(3 * j) + 1];
                indZ = inds[(3 * j) + 2];

                displeNode[(3 * j) + 0] = x[indX];
                displeNode[(3 * j) + 1] = x[indY];
                displeNode[(3 * j) + 2] = x[indZ];
            }

            return displeNode;
        }

        public override void SummForce_Calc(List<int> inds, VectorArray<double> nLoads, Vector<double> eLoads)
        {
            var vNumbs = inds.Count;

            var nNumbs = vNumbs / 3;
            for (int j = 0; j < nNumbs; j++)
            {
                var indX = inds[(3 * j) + 0];
                var indY = inds[(3 * j) + 1];
                var indZ = inds[(3 * j) + 2];

                nLoads[indX] = nLoads[indX] + eLoads[(3 * j) + 0];
                nLoads[indY] = nLoads[indY] + eLoads[(3 * j) + 1];
                nLoads[indZ] = nLoads[indZ] + eLoads[(3 * j) + 2];
            }
        }

        //public override VectorArray<double> GetIniDisplacements(NodeDofMap geo, int iter, VectorList<double> x)
        //{
        //    return new VectorArray<double>(geo.Count * 3);
        //    //var ini_displ = new double[geo.Count * 3];
        //    //x.Add(ini_displ);
        //}       
    }
}
