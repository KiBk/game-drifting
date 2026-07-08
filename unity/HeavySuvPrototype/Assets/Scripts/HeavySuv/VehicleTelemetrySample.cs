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
        public float pitchDegrees;
        public float rollDegrees;
        public DriveMode driveMode;
        public GearboxMode gearboxMode;
        public string activeGearLabel;
        public float engineTorque;
        public WheelTelemetry[] wheels;
    }
}
