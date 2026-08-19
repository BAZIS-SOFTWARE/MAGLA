using MaterialDB.MaterialData;
using MaterialDB.MaterialData.MetallurgicalData;
using PropertiesCalculator.PropertiesController.Interfaces;
using System;


namespace PropertiesCalculator.PropertiesController.HardnessModels
{
    /// <summary>
    /// Hardness for low alloy steel
    /// </summary>
    public class KurkinHardnessModels : IHardnessModel
    {
        public int BeiniteIndex { get; set; }
        public int FerriteIndex { get; set; }
        public int MartensiteIndex { get; set; }
        public int PerliteIndex { get; set; }
        public int AusteniteIndex { get; set; }
        /// <summary>
        /// BeiniteHardness
        /// </summary>
        public float BeiniteHardness { get; set; } = 300;
        /// <summary>
        /// MartensiteHardness
        /// </summary>
        public float MartensiteHardness { get; set; } = 400;
        /// <summary>
        /// PerliteHardness
        /// </summary>
        public float PerliteHardness { get; set; } = 200;
        /// <summary>
        /// FerriteHardness
        /// </summary>
        public float FerriteHardness { get; set; } = 250;
        /// <summary>
        /// AusteniteHardness
        /// </summary>
        public float AusteniteHardness { get; set; } = 150;

        /// <inheritdoc/>

        public float Calc(PhaseData phases)
        {
            return phases[FerriteIndex].Value * FerriteHardness + 
                phases[BeiniteIndex].Value * BeiniteHardness +
                phases[MartensiteIndex].Value * MartensiteHardness +
                phases[PerliteIndex].Value * PerliteHardness +
                phases[AusteniteIndex].Value * AusteniteHardness;
        }

        /// <summary>
        /// CalcFerriteHardness
        /// </summary>
        /// <param name="chemData"></param>
        /// <returns></returns>
        public void CalcFerriteHardness(ChemicalData chemData)
        {
            FerriteHardness = 100 + (301 * chemData[ChemElement.C]) + (20 * chemData[ChemElement.Mn]) + (25 * chemData[ChemElement.Si]) - (41 * chemData[ChemElement.Mo]) + (57 * chemData[ChemElement.Al]) - (24 * chemData[ChemElement.Cu]) + (35 * chemData[ChemElement.V]) + (176 * chemData[ChemElement.Ti]) - (6 * chemData[ChemElement.W]); // чистый Феррит
        }
        /// <summary>
        /// CalcPerliteHardness
        /// </summary>
        /// <param name="chemData"></param>
        /// <returns></returns>
        public void CalcPerliteHardness(ChemicalData chemData)
        {
            PerliteHardness = 100 + (301 * chemData[ChemElement.C]) + (20 * chemData[ChemElement.Mn]) + (25 * chemData[ChemElement.Si]) - (41 * chemData[ChemElement.Mo]) + (57 * chemData[ChemElement.Al]) - (24 * chemData[ChemElement.Cu]) + (35 * chemData[ChemElement.V]) + (176 * chemData[ChemElement.Ti]) - (6 * chemData[ChemElement.W]); // чистый перлит
        }
        /// <summary>
        /// CalcMartensiteHardness
        /// </summary>
        /// <param name="chemData"></param>
        /// <returns></returns>
        public void CalcMartensiteHardness(ChemicalData chemData)
        {
            MartensiteHardness = 262 + (977 * chemData[ChemElement.C]) - (301 * (float)Math.Pow(chemData[ChemElement.C], 2)) + (26 * chemData[ChemElement.Si]) + (9 * chemData[ChemElement.Ni]) + (24 * chemData[ChemElement.Mo]) + (8 * chemData[ChemElement.W]); // чистый Мартенсит
        }

        /// <summary>
        /// CalcBeiniteHardness
        /// </summary>
        /// <param name="chemData"></param>
        /// <returns></returns>
        public void CalcBeiniteHardness(ChemicalData chemData)
        {
            BeiniteHardness = 167 + (214 * chemData[ChemElement.C]) + (48 * chemData[ChemElement.Si]) + (35 * chemData[ChemElement.Cr]) + (28 * chemData[ChemElement.Ni]) + (133 * chemData[ChemElement.V]) + (105 * chemData[ChemElement.Al]) + (274 * chemData[ChemElement.Nb]); // чистый Бейнит
        }


    }
}
