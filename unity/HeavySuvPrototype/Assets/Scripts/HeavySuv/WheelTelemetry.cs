using UnityEngine;

namespace HeavySuvPrototype
{
    [System.Serializable]
    public struct WheelTelemetry
    {
        public string name;
        public bool isFront;
        public bool isLeft;
        public bool driven;
        public bool grounded;
        public float rpm;
        public float forwardSlip;
        public float sidewaysSlip;
        public float suspensionCompression;
        public Vector3 contactPoint;
    }
}
