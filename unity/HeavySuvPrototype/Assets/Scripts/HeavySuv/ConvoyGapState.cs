namespace HeavySuvPrototype
{
    [System.Serializable]
    public struct ConvoyGapState
    {
        public bool valid;
        public bool isTrailing;
        public float progressGapMeters;

        public static ConvoyGapState Invalid => new ConvoyGapState();
    }
}
