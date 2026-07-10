using UnityEngine;

namespace HeavySuvPrototype
{
    public static class CountersteerAssist
    {
        public static float CalculateTargetAngle(
            float slipAngleDegrees,
            float slipRateDegreesPerSecond,
            float yawRateDegreesPerSecond,
            float speedKmh,
            float minimumSpeedKmh,
            float engageSlipDegrees,
            float fullSlipDegrees,
            float maximumAngleDegrees,
            float slipGain,
            float slipRateGain,
            float yawDamping)
        {
            float slipMagnitude = Mathf.Abs(slipAngleDegrees);
            if (speedKmh <= minimumSpeedKmh || slipMagnitude <= engageSlipDegrees)
            {
                return 0f;
            }

            float speedBlend = Mathf.InverseLerp(
                minimumSpeedKmh,
                minimumSpeedKmh + 12f,
                speedKmh);
            float slipBlend = Mathf.InverseLerp(
                engageSlipDegrees,
                Mathf.Max(fullSlipDegrees, engageSlipDegrees + 0.1f),
                slipMagnitude);
            float targetAngle = slipAngleDegrees * slipGain +
                                slipRateDegreesPerSecond * slipRateGain -
                                yawRateDegreesPerSecond * yawDamping;
            if (targetAngle * slipAngleDegrees <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp(targetAngle, -maximumAngleDegrees, maximumAngleDegrees) *
                   speedBlend * slipBlend;
        }

        public static float StepTowardTarget(
            float currentAngle,
            float targetAngle,
            float responseDegreesPerSecond,
            float deltaTime)
        {
            return Mathf.MoveTowards(
                currentAngle,
                targetAngle,
                Mathf.Max(0f, responseDegreesPerSecond) * Mathf.Max(0f, deltaTime));
        }
    }
}
