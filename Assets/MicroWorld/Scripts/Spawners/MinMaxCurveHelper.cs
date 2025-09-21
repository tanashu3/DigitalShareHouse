using UnityEngine;

namespace MicroWorldNS.Spawners
{
    public static class MinMaxCurveHelper
    {
        public static float Value(this ParticleSystem.MinMaxCurve val, Rnd rnd)
        {
            var time = rnd.Float();
            var lerp = rnd.Float();
            var res = val.Evaluate(time, lerp);

            if (val.mode == ParticleSystemCurveMode.Curve || val.mode == ParticleSystemCurveMode.TwoCurves)
                res *= val.constant;

            return res;
        }

        public static int IntValue(this ParticleSystem.MinMaxCurve val, Rnd rnd)
        {
            return Mathf.RoundToInt(val.Value(rnd));
        }

        public static ParticleSystem.MinMaxCurve Clamp(this ParticleSystem.MinMaxCurve val, float min, float max)
        {
            val.constant = Mathf.Clamp(val.constant, min, max);
            val.constantMin = Mathf.Clamp(val.constantMin, min, max);
            val.constantMax = Mathf.Clamp(val.constantMax, min, max);
            return val;
        }

        public static ParticleSystem.MinMaxCurve ClampInt(this ParticleSystem.MinMaxCurve val, int min, int max)
        {
            val.constant = RoundInt(Mathf.Clamp(val.constant, min, max));
            val.constantMin = RoundInt(Mathf.Clamp(val.constantMin, min, max));
            val.constantMax = RoundInt(Mathf.Clamp(val.constantMax, min, max));
            return val;
        }

        static int RoundInt(float val)
        {
            var i = Mathf.RoundToInt(val);
            var d = val - i;
            if (d < 0.001f)
            {
                if (d >= 0f)
                    return i;
                else
                    return i - 1;
            }else
                return i + 1;
        }
    }
}
