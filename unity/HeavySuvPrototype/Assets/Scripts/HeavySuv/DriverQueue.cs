using System.Collections.Generic;

namespace HeavySuvPrototype
{
    public sealed class DriverQueue
    {
        private readonly ulong?[] driverSlots;
        private readonly List<ulong> spectators = new List<ulong>();

        public DriverQueue(int driverCount = 8, int capacity = 8)
        {
            DriverCount = driverCount;
            Capacity = capacity;
            driverSlots = new ulong?[driverCount];
        }

        public int DriverCount { get; }
        public int Capacity { get; }
        public int Count { get; private set; }
        public IReadOnlyList<ulong> Spectators => spectators;

        public bool Add(ulong clientId)
        {
            if (Count >= Capacity || Contains(clientId))
            {
                return false;
            }

            int openSlot = FirstOpenSlot();
            if (openSlot >= 0)
            {
                driverSlots[openSlot] = clientId;
            }
            else
            {
                spectators.Add(clientId);
            }

            Count += 1;
            return true;
        }

        public ulong? Remove(ulong clientId)
        {
            for (int slot = 0; slot < driverSlots.Length; slot += 1)
            {
                if (driverSlots[slot] != clientId)
                {
                    continue;
                }

                driverSlots[slot] = null;
                Count -= 1;
                if (spectators.Count == 0)
                {
                    return null;
                }

                ulong promoted = spectators[0];
                spectators.RemoveAt(0);
                driverSlots[slot] = promoted;
                return promoted;
            }

            if (spectators.Remove(clientId))
            {
                Count -= 1;
            }

            return null;
        }

        public MultiplayerRole GetRole(ulong clientId)
        {
            return GetDriverSlot(clientId) >= 0 ? MultiplayerRole.Driver : MultiplayerRole.Spectator;
        }

        public int GetDriverSlot(ulong clientId)
        {
            for (int slot = 0; slot < driverSlots.Length; slot += 1)
            {
                if (driverSlots[slot] == clientId)
                {
                    return slot;
                }
            }

            return -1;
        }

        public int GetQueuePosition(ulong clientId)
        {
            int index = spectators.IndexOf(clientId);
            return index < 0 ? 0 : index + 1;
        }

        public ulong? GetDriver(int slot)
        {
            return slot >= 0 && slot < driverSlots.Length ? driverSlots[slot] : null;
        }

        public void Clear()
        {
            for (int slot = 0; slot < driverSlots.Length; slot += 1)
            {
                driverSlots[slot] = null;
            }

            spectators.Clear();
            Count = 0;
        }

        private bool Contains(ulong clientId)
        {
            return GetDriverSlot(clientId) >= 0 || spectators.Contains(clientId);
        }

        private int FirstOpenSlot()
        {
            for (int slot = 0; slot < driverSlots.Length; slot += 1)
            {
                if (!driverSlots[slot].HasValue)
                {
                    return slot;
                }
            }

            return -1;
        }
    }
}
