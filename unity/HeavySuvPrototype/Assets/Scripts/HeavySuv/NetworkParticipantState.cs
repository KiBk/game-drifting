using System;
using Unity.Netcode;

namespace HeavySuvPrototype
{
    public struct NetworkParticipantState : INetworkSerializable, IEquatable<NetworkParticipantState>
    {
        public ulong clientId;
        public MultiplayerRole role;
        public int queuePosition;
        public int driverSlot;
        public NetworkObjectReference car;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref role);
            serializer.SerializeValue(ref queuePosition);
            serializer.SerializeValue(ref driverSlot);
            serializer.SerializeValue(ref car);
        }

        public bool Equals(NetworkParticipantState other)
        {
            return clientId == other.clientId &&
                role == other.role &&
                queuePosition == other.queuePosition &&
                driverSlot == other.driverSlot &&
                car.Equals(other.car);
        }
    }
}
