using MaterialDB.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace PropertiesCalculator
{
    /// <summary>
    /// LoadMaterialDataBaseFromTextFormat
    /// </summary>
    public class LoadMaterialDataBaseFromTextFormat : ILoader
    {
        List<string> generalProps = new List<string>()
            {
                "Структура",
                "Кристаллизация",
                "Модель упрочнения",
                "Модель твердости",
                "Химический состав",
                "Размер аустенитного зерна",
                "Модель роста аустенитного зерна"
            };
        List<string> heatProps = new List<string>()
            {
                "Плотность",
                "Теплопроводность",
                "Теплоемкость"
            };
        List<string> mechProps = new List<string>()
            {
                "Модуль Юнга",
                "Коэффициент Пуассона",
                "Коэффициент упрочнения",
                "Предел текучести",
                "Предел прочности",
                "ТКЛР",
                "Релаксация",
            };
        /// <summary>
        /// LoadDataBase
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public DataSet LoadDataBase(string path)
        {
            using (var sreamReader = new StreamReader(path))
            {
                var dataSet = new DataSet();
                dataSet.DataSetName = "Материалы";

                var words = new List<string>(sreamReader.ReadToEnd().Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));

                var startInd = words.FindIndex(x => x == "Список");
                var stopInd = words.FindIndex(x => x == "#Список");

                var materials = LoadMaterialNames(startInd, stopInd, words);
                PropertyItem propertyItem;

                for (int i = 0; i < materials.Count; i++)
                {
                    var matList = new List<DataTable>();
                    startInd = words.FindLastIndex(x => x == materials[i]);
                    stopInd = words.FindLastIndex(x => x == "#" + materials[i]);

                    var matAr = new string[stopInd - (startInd + 1)];
                    Array.Copy(words.ToArray(), startInd + 1, matAr, 0, stopInd - (startInd + 1));

                    foreach (var prop in generalProps)
                    {
                        if (CheckProp(matAr, prop, out propertyItem))
                        {
                            var table = new DataTable(string.Format("{0},{1},{2},{3}",
                                materials[i], "Общие сведения", propertyItem.Name, propertyItem.Units));
                            LoadGeneralProperties(table, matAr, propertyItem);
                            matList.Add(table);
                        }
                    }
                    foreach (var prop in heatProps)
                    {
                        if (CheckProp(matAr, prop, out propertyItem))
                        {
                            var table = new DataTable(string.Format("{0},{1},{2},{3}",
        materials[i], "Тепловые свойства", propertyItem.Name, propertyItem.Units));

                            LoadPhysicalProperties(matList[0], table, matAr, propertyItem);
                            matList.Add(table);
                        }
                    }
                    foreach (var prop in mechProps)
                    {
                        if (CheckProp(matAr, prop, out propertyItem))
                        {
                            var table = new DataTable(string.Format("{0},{1},{2},{3}",
                                materials[i], "Механические свойства", propertyItem.Name, propertyItem.Units));

                            LoadPhysicalProperties(matList[0], table, matAr, propertyItem);
                            matList.Add(table);
                        }

                    }

                    if (CheckProp(matAr, "Металлургия", out propertyItem))
                    {
                        var reacTables = LoadMaterialReac(materials[i], matAr, propertyItem);
                        matList.AddRange(reacTables.ToArray());
                    }
                    dataSet.Tables.AddRange(matList.ToArray());
                }
                return dataSet;
            }
                
        }
        /// <summary>
        /// CheckProp
        /// </summary>
        /// <param name="matAr"></param>
        /// <param name="propName"></param>
        /// <param name="propertyItem"></param>
        /// <returns></returns>
        public bool CheckProp(string [] matAr, string propName, out PropertyItem propertyItem)
        {
            try
            {
                var propNameUnits = matAr.First(x => x.Split(',')[0] == propName);

                if (propNameUnits == "") throw new Exception("Свойство " + propName + " не найдено!");

                var startInd = Array.IndexOf(matAr, propNameUnits);
                if (startInd == -1) throw new Exception("Точка начала данных свойства" + propName + " не найдена!");
                var stopInd = Array.IndexOf(matAr, "#" + propName);
                if (stopInd == -1) throw new Exception("Точка окончания данных свойства" + propName + " не найдена!");

                var propNameUnitsAr = propNameUnits.Split(',');

                if (propNameUnitsAr.Length == 1)
                    propertyItem = new PropertyItem(propName, startInd, stopInd);
                else
                    propertyItem = new PropertyItem(propNameUnitsAr[0], propNameUnitsAr[1], startInd, stopInd);

                return true;
            }
            catch (Exception)
            {
                propertyItem = new PropertyItem();
                return false;
            }

        }
        /// <summary>
        /// LoadMaterialNames
        /// </summary>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <param name="words"></param>
        /// <returns></returns>
        private List<string> LoadMaterialNames(int start, int stop, List<string> words)
        {
            var materials = new List<string>();
            for (int i = start + 1; i < stop; i++)
            {
                var mat = words[i].Split(' ').Where(x => x != "").ToArray();
                materials.Add(mat[0]);
            }
            return materials;
        }
        /// <summary>
        /// LoadPhysicalProperties
        /// </summary>
        /// <param name="phaseTable"></param>
        /// <param name="table"></param>
        /// <param name="matAr"></param>
        /// <param name="propertyItem"></param>
        private void LoadPhysicalProperties(DataTable phaseTable, DataTable table, string[] matAr, PropertyItem propertyItem)
        {
            var columnTemp = new DataColumn("Температура", typeof(float)) { DefaultValue = 0 };
            table.Columns.Add(columnTemp);
            var phaseIndex = 0;
            for (int j = propertyItem.StartInd + 1; j < propertyItem.StopInd; j++)
            {

                var structAr = matAr[j].Split(new char[] { ' ', ',' }).Where(x => x != "").ToArray();
                
                var phaseName = phaseTable.Rows[phaseIndex][0].ToString();
                var columnPhase= new DataColumn(phaseName, typeof(float)) { DefaultValue = 0 };
                table.Columns.Add(columnPhase);

                var dataAr = matAr[j].Split(new char[] { ' ', ',', '\t' }).Where(x => x != "" && x != " ").ToArray();

                for (int m = 0; m < dataAr.Length / 2; m++)
                {
                    
                    if (table.Rows.Count <= m)
                    {
                        var newRow = table.NewRow();
                        newRow[phaseName] = float.Parse(dataAr[(2 * m) + 1]);
                        newRow["Температура"] = float.Parse(dataAr[(2 * m) + 0]);
                        table.Rows.Add(newRow);
                    }
                    else
                    {
                        table.Rows[m]["Температура"] = float.Parse(dataAr[(2 * m) + 0]);
                        table.Rows[m][phaseName] = float.Parse(dataAr[(2 * m) + 1]);
                    }
                }
                phaseIndex++;
            }
        }

        /// <summary>
        /// LoadGeneralProperties
        /// </summary>
        /// <param name="table"></param>
        /// <param name="matAr"></param>
        /// <param name="propertyItem"></param>
        private void LoadGeneralProperties(DataTable table, string[] matAr, PropertyItem propertyItem)
        {
            if(propertyItem.Name == "Модель упрочнения" | 
                propertyItem.Name == "Модель твердости" )
            {
                table.Columns.Add(propertyItem.Name, typeof(int));
  
                var structAr = matAr[propertyItem.StartInd + 1].Split(new char[] { ' ', ',' }).Where(x => x != "").ToArray();
                var newRow = table.NewRow();
                var num = int.Parse(structAr[0]);
                newRow[propertyItem.Name] = num;
                table.Rows.Add(newRow);
            }
            else if(propertyItem.Name == "Размер аустенитного зерна")
            {
                table.Columns.Add(propertyItem.Name, typeof(float));

                var structAr = matAr[propertyItem.StartInd + 1].Split(new char[] { ' ', ',' }).Where(x => x != "").ToArray();
                var newRow = table.NewRow();
                var num = float.Parse(structAr[0]);
                newRow[propertyItem.Name] = num;
                table.Rows.Add(newRow);
            }
            else
            {

                var propName = propertyItem.Units.Split('-')[0];
                var valName = propertyItem.Units.Split('-')[1];

                table.Columns.Add(propName, typeof(string));
                table.Columns.Add(valName, typeof(float));

                for (int j = propertyItem.StartInd + 1; j < propertyItem.StopInd; j++)
                {
                    var structAr = matAr[j].Split(new char[] { ' ', ',' }).Where(x => x != "").ToArray();

                    for (int m = 0; m < structAr.Length / 2; m++)
                    {
                        var newRow = table.NewRow();
                        newRow[propName] = structAr[(2 * m) + 0];
                        newRow[valName] = float.Parse(structAr[(2 * m) + 1]);
                        table.Rows.Add(newRow);
                    }
                }
            }
        }
        /// <summary>
        /// LoadMaterialReac
        /// </summary>
        /// <param name="material"></param>
        /// <param name="matAr"></param>
        /// <param name="propertyItem"></param>
        /// <returns></returns>
        private List<DataTable> LoadMaterialReac(string material, string[] matAr, PropertyItem propertyItem)
        {

            var tableList = new List<DataTable>();

            for (int i = propertyItem.StartInd + 1; i < propertyItem.StopInd; i++)
            {
                var reacData = matAr[i];
                var tableName = string.Format("{0},{1},{2}", material, propertyItem.Name, reacData);
                var table = new DataTable(tableName);
                table.Columns.Add("Температура", typeof(float));
                table.Columns.Add("Доля", typeof(float));

                var startInd = Array.FindLastIndex(matAr, x => x == reacData);
                var stopInd = Array.FindLastIndex(matAr, x => x == "#" + reacData.Split(',')[0]);

                //var dataAr = matAr[i + 1].Split(new char[] { ',' }).Where(x => x != "" && x != " ").ToArray();

                for (int m = startInd + 1; m < stopInd; m++)
                {
                    var subDataAr = matAr[m].Split(new char[] { ' ' }).Where(x => x != "" && x != " ").ToArray();

                    var newRow = table.NewRow();
                    newRow["Доля"] = float.Parse(subDataAr[1]);
                    newRow["Температура"] = float.Parse(subDataAr[0]);

                    for (int j = 2; j < subDataAr.Length; j = j + 2)
                    {
                        if (!table.Columns.Contains("Скорость " + subDataAr[j]))
                            table.Columns.Add("Скорость " + subDataAr[j], typeof(float));

                        newRow["Скорость " + subDataAr[j]] = float.Parse(subDataAr[j + 1]);
                    }
                    table.Rows.Add(newRow);

                }

                tableList.Add(table);
                i += stopInd - startInd;
            }
            return tableList;

        }
    }
}
