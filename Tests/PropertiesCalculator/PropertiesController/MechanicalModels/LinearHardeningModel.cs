using PropertiesCalculator.PropertiesController.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertiesCalculator.PropertiesCalculator.MechanicalModels
{
    public class LinearHardeningModel : IHardeningModel<float>
    {
        public float Calc(float yield, float slope, float tensile, float eqEp)
        {
            var max_eqSig = tensile - yield;
            var max_eqEp = max_eqSig * slope;


            return max_eqSig * eqEp / max_eqEp;
        }
    }
}
