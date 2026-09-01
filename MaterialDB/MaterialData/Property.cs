using MaterialDB.Interfaces;
using MaterialDB.MaterialData.MetallurgicalData;
using MaterialDB.Utilities;
using System;
using System.Data;
using System.Linq;

namespace MaterialDB.MaterialData
{
    /// <summary>
    /// Property
    /// </summary>
    [Serializable]
    public class Property : IProperty
    {
        public double this[int row,int col]
        {
            get { return Convert.ToDouble(DataTable.Rows[row][col]); }
            set { DataTable.Rows[row][col] = value; }
        }


        /// <inheritdoc/>
        public string Name 
        {
            get;
            set;
        }
/// <inheritdoc/>

        public string X_unit { get; set; }
/// <inheritdoc/>


        public string Y_unit { get; set; }
/// <inheritdoc/>


        public string Units { get;set; }
/// <inheritdoc/>

        public DataTable DataTable 
        { 
            get; 
            set; 
        }
/// <inheritdoc/>
        public override string ToString()
        {
            return $"{Name},{Units}";
        }

        public Property(string name, string x_unit, string y_unit, string units, DataTable dataTable)
        {
            Name = name;
            X_unit = x_unit;
            Y_unit = y_unit;
            Units = units;
            DataTable = dataTable;
        }

        /// <inheritdoc/>
        public Property Copy(string copyName)
        {
            var newProperty = new Property(copyName, X_unit, Y_unit, Units, DataTable.Copy());

            return newProperty;
        }
/// <inheritdoc/>
        public float CalcProp(PhaseData phaseData, float temp)
        {
            if (DataTable.Columns.Count - 1 != phaseData.Count)
                throw new Exception(
   $"The phase count does not match the phase count of property {Name}. Expected {DataTable.Columns.Count}.");

            var xAr = DataTable.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[0])).ToArray();
            var yn = 0.0f;
            for (int j = 1; j < DataTable.Columns.Count; j++)
            {
                var yAr = DataTable.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[j])).ToArray();              
                var yv = yAr.Length == 1 ? 
                    yAr[0] : 
                    InterpolationSearch.InterpolatedValue(xAr, yAr, temp);
                yn = yn + yv * phaseData[j - 1].Value;
            }
            return yn;
        }
/// <inheritdoc/>
        public float CalcProp(float variable)
        {
            var xAr = DataTable.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[0])).ToArray();
            var yAr = DataTable.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[1])).ToArray();
            return InterpolationSearch.InterpolatedValue(xAr, yAr, variable);
        }
/// <inheritdoc/>

        public float CalcProp(DataTable phaseData, float temp)
        {
            if (DataTable.Columns.Count - 1 != phaseData.Rows.Count)
                throw new Exception(
   $"The phase count does not match the phase count of property {Name}. Expected {DataTable.Columns.Count}.");

            var xAr = DataTable.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[0])).ToArray();
            var yn = 0.0f;
            for (int j = 1; j < DataTable.Columns.Count; j++)
            {
                var yAr = DataTable.Rows.Cast<DataRow>().Select(r => Convert.ToSingle(r[j])).ToArray();
                var yv = InterpolationSearch.InterpolatedValue(xAr, yAr, temp);
                yn = yn + yv * Convert.ToSingle(phaseData.Rows[j - 1]);
            }
            return yn;
        }

        public void AddRow()
        {
            var row = DataTable.NewRow();
            DataTable.Rows.Add(row);
        }
    }
}
