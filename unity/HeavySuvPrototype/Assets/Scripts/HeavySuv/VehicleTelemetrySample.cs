using UnityEngine;

namespace HeavySuvPrototype
{
    [System.Serializable]
    public struct VehicleTelemetrySample
    {
        public Vector3 position;
        public float signedSpeedMetersPerSecond;
        public float speedKmh;
        public float headingDegrees;
        public float slipAngleDegrees;
        public float pitchDegrees;
        public float rollDegrees;
        public DriveMode driveMode;
        public DriveSelectorMode selectorMode;
        public string activeSelectorLabel;
        public float motorTorque;
        public WheelTelemetry[] wheels;
    }
}
