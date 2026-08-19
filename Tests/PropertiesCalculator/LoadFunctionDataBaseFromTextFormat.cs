using MaterialDB.Interfaces;
using System.Data;
using System.IO;
using System.Linq;

namespace PropertiesCalculator
{
    /// <summary>
    /// LoadFunctionDataBaseFromTextFormat
    /// </summary>
    public class LoadFunctionDataBaseFromTextFormat : ILoader
    {
        /// <summary>
        /// LoadDataBase
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public DataSet LoadDataBase(string path)
        {
            using (var reader = new StreamReader(path))
            {
                var dataSet = new DataSet();
                while (!reader.EndOfStream)
                {
                    var words = reader.ReadLine().Split(new char[] { ',' });
                    if (words[0] == "Список")
                    {
                        words = reader.ReadLine().Split(new char[] { ',' });
                        while (words[0] != "#Список")
                        {
                            var tableName = words[0] + "," + words[1];
                            dataSet.Tables.Add(tableName);
                            dataSet.Tables[tableName].Columns.Add("X", typeof(float));

                            words = reader.ReadLine().Split(new char[] { ',' });
                        }
                    }

                    if (words.Count() > 1)
                    {
                        var tableName = words[0] + "," + words[1];
                        if (dataSet.Tables.IndexOf(tableName) != -1)
                        {
                            var table = dataSet.Tables[tableName];
                            var property = words[0];

                            table.Columns.Add("Y", typeof(float));
                            words = reader.ReadLine().Split(new char[] { }).Where(x => x != "" & x != " ").ToArray();

                            while (words[0] != "#" + property)
                            {
                                DataRow workRow = table.NewRow();
                                workRow["X"] = float.Parse(words[0]);
                                workRow["Y"] = float.Parse(words[1]);

                                table.Rows.Add(workRow);

                                words = reader.ReadLine().Split(new char[] { }).Where(x => x != "").ToArray();
                            }

                        }
                    }

                }
                return dataSet;
            }                
        }
    }
}
