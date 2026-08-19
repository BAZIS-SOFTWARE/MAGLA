using MaterialDB.Interfaces;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace PropertiesCalculator
{
    public class SaveMaterialDataBaseToTextFormat : ISaver
    {
        public void SaveDataBase(DataSet data, string path)
        {
            var materials = new Dictionary<string, Dictionary<string, List<string>>>();
            var units = new Dictionary<string, string>();

            using (var materialFile = new StreamWriter(path, false))
            {
                materialFile.WriteLine("Список");
                WriteMaterialsList(data, materials, units, materialFile);
                materialFile.WriteLine("#Список\n");

                foreach (var material in materials)
                {
                    materialFile.WriteLine(material.Key);
                    foreach (var cat in material.Value)
                    {
                        materialFile.WriteLine(cat.Key);
                        foreach (var prop in cat.Value)
                        {
                            materialFile.WriteLine(prop + "," + units[prop]);
                            var key = string.Join(",", new string[] { material.Key, cat.Key, prop, units[prop] });
                            var table = data.Tables[key];
                            if (cat.Key == "Общие сведения")
                                SaveGeneralProperties(table, materialFile);
                            if (cat.Key == "Тепловые свойства" | cat.Key == "Механические свойства")
                                SavePhysicalProperties(table, materialFile);
                            if (cat.Key == "Металлургия")
                            {
                                SaveMetallurgicalProperties(table, materialFile);
                            }

                            materialFile.WriteLine("#" + prop);
                        }
                        materialFile.WriteLine("#" + cat.Key);
                    }
                    materialFile.WriteLine("#" + material.Key);
                }
            }              
        }

        private static void WriteMaterialsList(DataSet data, Dictionary<string, Dictionary<string, List<string>>> materials, Dictionary<string, string> units, StreamWriter materialFile)
        {
            foreach (DataTable table in data.Tables)
            {
                var matAr = table.TableName.Split(',');
                var matName = matAr[0];
                var matCat = matAr[1];
                var matProp = matAr[2];
                var propUnits = matAr[3];

                if (!units.ContainsKey(matProp))
                    units.Add(matProp, propUnits);

                if (!materials.ContainsKey(matName))
                {
                    materialFile.WriteLine(matName);
                    materials.Add(matName, new Dictionary<string, List<string>>() { { matCat, new List<string>() { matProp } } });
                }

                else
                {
                    if (!materials[matName].ContainsKey(matCat))
                        materials[matName].Add(matCat, new List<string>() { matProp });
                    else materials[matName][matCat].Add(matProp);
                }
            }
        }

        private void SaveMetallurgicalProperties(DataTable table, StreamWriter materialFile)
        {
            var strAr = new List<string>();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                var subStrAr = new List<string>();
                for (int j = 0; j < table.Columns.Count; j++)
                {
                    if(j < 2)
                        subStrAr.Add(table.Rows[i][j].ToString());
                    else                      
                        subStrAr.Add(table.Columns[j].ColumnName.Split(' ','_')[1] + " " + table.Rows[i][j].ToString());


                }

                materialFile.WriteLine(string.Join(" ", subStrAr));
            }
            //materialFile.WriteLine(string.Join(",", strAr));
        }

        private void SavePhysicalProperties(DataTable table, StreamWriter materialFile)
        {
            for (int i = 1; i < table.Columns.Count; i++)
            {
                var strAr = new List<string>();
                for (int j = 0; j < table.Rows.Count; j++)
                {
                    var temp = table.Rows[j][0];
                    var prop = table.Rows[j][i];

                    strAr.Add(string.Join(" ",new object[] { temp, prop }));

                }
                materialFile.WriteLine(string.Join(" ", strAr));
            }
        }

        private static void SaveGeneralProperties(DataTable table, StreamWriter materialFile)
        {
            if (table.Columns.Count == 1)
                materialFile.WriteLine(table.Rows[0][0].ToString());
            else
            {
                var strAr = new List<string>();
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    strAr.Add(string.Join(" ", table.Rows[i].ItemArray));

                }
                materialFile.WriteLine(string.Join(" ", strAr));
            }
        }       
    }
}
