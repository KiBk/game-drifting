using UnityEngine;

namespace HeavySuvPrototype
{
    public struct DifferentialTorqueSplit
    {
        public float firstTorque;
        public float secondTorque;
    }

    public static class RacingDifferential
    {
        public static DifferentialTorqueSplit SplitTorque(
            float totalTorque,
            float nominalFirstShare,
            float firstTraction,
            float secondTraction,
            float lockStrength,
            float maximumBiasRatio,
            float tractionDifferenceDeadband = 0f)
        {
            float difference = Mathf.Clamp01(firstTraction) - Mathf.Clamp01(secondTraction);
            float deadband = Mathf.Clamp(tractionDifferenceDeadband, 0f, 0.99f);
            float differenceMagnitude = Mathf.Abs(difference);
            if (differenceMagnitude <= deadband)
            {
                difference = 0f;
            }
            else
            {
                difference = Mathf.Sign(difference) *
                    (differenceMagnitude - deadband) / (1f - deadband);
            }

            float ratio = Mathf.Max(1f, maximumBiasRatio);
            float minimumShare = 1f / (1f + ratio);
            float maximumShare = ratio / (1f + ratio);
            float desiredFirstShare = Mathf.Clamp01(nominalFirstShare) +
                                      difference * 0.5f * Mathf.Clamp01(lockStrength);
            float firstShare = Mathf.Clamp(desiredFirstShare, minimumShare, maximumShare);
            float firstTorque = totalTorque * firstShare;
            return new DifferentialTorqueSplit
            {
                firstTorque = firstTorque,
                secondTorque = totalTorque - firstTorque
            };
        }

        public static float EffectiveTraction(
            float firstTraction,
            float secondTraction,
            float lockStrength,
            float maximumBiasRatio)
        {
            float lowerTraction = Mathf.Min(
                Mathf.Clamp01(firstTraction),
                Mathf.Clamp01(secondTraction));
            float higherTraction = Mathf.Max(
                Mathf.Clamp01(firstTraction),
                Mathf.Clamp01(secondTraction));
            float ratio = Mathf.Max(1f, maximumBiasRatio);
            float lockedTraction = (higherTraction * ratio + lowerTraction) / (ratio + 1f);
            return Mathf.Lerp(lowerTraction, lockedTraction, Mathf.Clamp01(lockStrength));
        }
    }
}
