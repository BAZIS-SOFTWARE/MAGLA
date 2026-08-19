using PropertiesCalculator.PropertiesController.Interfaces;
using System;

namespace PropertiesCalculator.PropertiesController.MechanicalModels
{
    public class ExponentialHardeningModel : IHardeningModel<float>
    {
        public float Calc(float yield, float slope, float tensile, float eqEp)
        {
            var res = (tensile - yield) * (1 - Math.Exp(-slope * eqEp));
            return (float)res;
        }
    }
}
