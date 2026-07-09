namespace HeavySuvPrototype
{
    public sealed class MultiplayerReadinessGate
    {
        public bool SessionReady { get; private set; }
        public bool NetworkReady { get; private set; }
        public bool IsReady => SessionReady && NetworkReady;

        public void MarkSessionReady()
        {
            SessionReady = true;
        }

        public void MarkNetworkReady()
        {
            NetworkReady = true;
        }

        public void Reset()
        {
            SessionReady = false;
            NetworkReady = false;
        }
    }
}
