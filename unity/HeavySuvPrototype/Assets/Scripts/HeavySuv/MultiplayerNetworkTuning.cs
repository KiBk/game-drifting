using Unity.Netcode.Components;

namespace HeavySuvPrototype
{
    public static class MultiplayerNetworkTuning
    {
        public const uint TickRate = 50;
        public const float PositionThreshold = 0.01f;
        public const float RotationThreshold = 0.1f;
        public const float InterpolationSmoothing = 0.08f;

        public static void Apply(NetworkTransform networkTransform)
        {
            if (networkTransform == null)
            {
                return;
            }

            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.AutoOwnerAuthorityTickOffset = true;
            networkTransform.PositionInterpolationType = NetworkTransform.InterpolationTypes.Lerp;
            networkTransform.RotationInterpolationType = NetworkTransform.InterpolationTypes.Lerp;
            networkTransform.PositionLerpSmoothing = true;
            networkTransform.PositionMaxInterpolationTime = InterpolationSmoothing;
            networkTransform.RotationLerpSmoothing = true;
            networkTransform.RotationMaxInterpolationTime = InterpolationSmoothing;
            networkTransform.Interpolate = true;
            networkTransform.UseUnreliableDeltas = true;
            networkTransform.SyncPositionX = true;
            networkTransform.SyncPositionY = true;
            networkTransform.SyncPositionZ = true;
            networkTransform.SyncRotAngleX = true;
            networkTransform.SyncRotAngleY = true;
            networkTransform.SyncRotAngleZ = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;
            networkTransform.PositionThreshold = PositionThreshold;
            networkTransform.RotAngleThreshold = RotationThreshold;
            networkTransform.UseQuaternionSynchronization = true;
            networkTransform.UseQuaternionCompression = true;
            networkTransform.UseHalfFloatPrecision = true;
            networkTransform.SlerpPosition = false;
        }
    }
}
