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

        public static VehicleInputState Merge(VehicleInputState primary, VehicleInputState secondary)
        {
            return new VehicleInputState
            {
                throttle = primary.throttle || secondary.throttle,
                brake = primary.brake || secondary.brake,
                steerLeft = primary.steerLeft || secondary.steerLeft,
                steerRight = primary.steerRight || secondary.steerRight,
                handbrake = primary.handbrake || secondary.handbrake,
                turbo = primary.turbo || secondary.turbo,
                toggleDriveMode = primary.toggleDriveMode || secondary.toggleDriveMode
            };
        }
    }
}
