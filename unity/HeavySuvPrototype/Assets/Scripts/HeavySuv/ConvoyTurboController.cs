using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class ConvoyTurboController : MonoBehaviour
    {
        [Header("Prototype")]
        public bool alwaysAvailable = true;

        [Header("Gap thresholds")]
        public float disengageGapMeters = 5f;
        public float chargeStartGapMeters = 10f;
        public float maximumChargeGapMeters = 30f;

        [Header("Energy")]
        public float fullChargeTimeAtMaximumGap = 5f;
        public float boostCapacitySeconds = 2.5f;

        [Header("Delivery")]
        public float maximumTorqueMultiplier = 1.65f;
        public float rampUpSeconds = 0.28f;
        public float rampDownSeconds = 0.38f;

        private ConvoyGapState gapState;
        private float charge01;
        private float torqueMultiplier = 1f;

        public ConvoyGapState GapState => gapState;
        public float Charge01 => alwaysAvailable ? 1f : charge01;
        public bool IsEligible => alwaysAvailable ||
            (gapState.valid && gapState.isTrailing && gapState.progressGapMeters > disengageGapMeters);
        public bool IsActive { get; private set; }
        public float TorqueMultiplier => torqueMultiplier;

        public void SetGapState(ConvoyGapState state)
        {
            state.progressGapMeters = Mathf.Max(0f, state.progressGapMeters);
            gapState = state;
        }

        public void Step(float deltaTime, bool turboPressed)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            bool activationRequested = turboPressed && IsEligible && Charge01 > 0f;

            if (!alwaysAvailable && !activationRequested && CanCharge())
            {
                float gapFactor = Mathf.InverseLerp(
                    chargeStartGapMeters,
                    maximumChargeGapMeters,
                    gapState.progressGapMeters);
                float chargeRate = gapFactor / Mathf.Max(fullChargeTimeAtMaximumGap, 0.01f);
                charge01 = Mathf.Clamp01(charge01 + chargeRate * safeDeltaTime);
            }

            float targetMultiplier = activationRequested
                ? maximumTorqueMultiplier
                : 1f;
            float rampSeconds = targetMultiplier > torqueMultiplier ? rampUpSeconds : rampDownSeconds;
            float rampSpeed = (maximumTorqueMultiplier - 1f) / Mathf.Max(rampSeconds, 0.01f);
            torqueMultiplier = Mathf.MoveTowards(torqueMultiplier, targetMultiplier, rampSpeed * safeDeltaTime);

            IsActive = activationRequested && torqueMultiplier > 1.01f;
            if (IsActive && !alwaysAvailable)
            {
                float deliveredBoostFraction = Mathf.InverseLerp(1f, maximumTorqueMultiplier, torqueMultiplier);
                charge01 = Mathf.Clamp01(
                    charge01 - deliveredBoostFraction * safeDeltaTime / Mathf.Max(boostCapacitySeconds, 0.01f));
            }
        }

        private bool CanCharge()
        {
            return gapState.valid &&
                gapState.isTrailing &&
                gapState.progressGapMeters > chargeStartGapMeters &&
                charge01 < 1f;
        }
    }
}
