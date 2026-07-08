namespace HeavySuvPrototype
{
    [System.Serializable]
    public struct VehicleInputState
    {
        public bool throttle;
        public bool brake;
        public bool steerLeft;
        public bool steerRight;
        public bool handbrake;
        public bool turbo;
        public bool toggleDriveMode;

        public static VehicleInputState None => new VehicleInputState();
    }
}
