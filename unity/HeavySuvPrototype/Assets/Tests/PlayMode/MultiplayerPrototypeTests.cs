using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HeavySuvPrototype.Tests
{
    public sealed class MultiplayerPrototypeTests
    {
        [Test]
        public void FirstTwoParticipantsDriveAndRemainingParticipantsQueue()
        {
            DriverQueue queue = new DriverQueue();
            for (ulong clientId = 1; clientId <= 8; clientId += 1)
            {
                Assert.IsTrue(queue.Add(clientId));
            }

            Assert.AreEqual(MultiplayerRole.Driver, queue.GetRole(1));
            Assert.AreEqual(MultiplayerRole.Driver, queue.GetRole(2));
            Assert.AreEqual(0, queue.GetDriverSlot(1));
            Assert.AreEqual(1, queue.GetDriverSlot(2));
            Assert.AreEqual(1, queue.GetQueuePosition(3));
            Assert.AreEqual(6, queue.GetQueuePosition(8));
            Assert.IsFalse(queue.Add(9));
        }

        [Test]
        public void DriverDeparturePromotesFirstSpectatorIntoSameSlot()
        {
            DriverQueue queue = CreateFourParticipantQueue();

            ulong? promoted = queue.Remove(1);

            Assert.AreEqual(3ul, promoted);
            Assert.AreEqual(0, queue.GetDriverSlot(3));
            Assert.AreEqual(MultiplayerRole.Driver, queue.GetRole(3));
            Assert.AreEqual(1, queue.GetQueuePosition(4));
            Assert.AreEqual(3, queue.Count);
        }

        [Test]
        public void SpectatorDepartureCompactsQueuePositions()
        {
            DriverQueue queue = CreateFourParticipantQueue();

            Assert.IsNull(queue.Remove(3));

            Assert.AreEqual(1, queue.GetQueuePosition(4));
            Assert.AreEqual(3, queue.Count);
        }

        [Test]
        public void VehiclePaletteColorsRoundTripAndRemainDistinct()
        {
            Assert.GreaterOrEqual(MultiplayerCoordinator.VehiclePalette.Length, 2);
            uint first = NetworkRallyCar.PackColor(MultiplayerCoordinator.VehiclePalette[0]);
            uint second = NetworkRallyCar.PackColor(MultiplayerCoordinator.VehiclePalette[1]);

            Assert.AreNotEqual(first, second);
            Assert.AreEqual(MultiplayerCoordinator.VehiclePalette[0], NetworkRallyCar.UnpackColor(first));
        }

        [Test]
        public void NetworkCarPrefabUsesOwnerAuthorityAndGhostLayer()
        {
            GameObject prefab = Resources.Load<GameObject>("Network/NetworkRallyCar");
            GameObject coordinatorObject = new GameObject("Coordinator Test");
            coordinatorObject.AddComponent<NetworkObject>();
            coordinatorObject.AddComponent<MultiplayerCoordinator>();

            Assert.NotNull(prefab);
            Assert.NotNull(prefab.GetComponent<NetworkObject>());
            Assert.NotNull(prefab.GetComponent<NetworkRigidbody>());
            Assert.NotNull(prefab.GetComponent<NetworkRallyCar>());
            Assert.AreEqual(MultiplayerCoordinator.NetworkVehicleLayer, prefab.layer);
            Assert.AreEqual(
                NetworkTransform.AuthorityModes.Owner,
                prefab.GetComponent<NetworkTransform>().AuthorityMode);
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(
                MultiplayerCoordinator.NetworkVehicleLayer,
                MultiplayerCoordinator.NetworkVehicleLayer));

            Object.DestroyImmediate(coordinatorObject);
        }

        private static DriverQueue CreateFourParticipantQueue()
        {
            DriverQueue queue = new DriverQueue();
            queue.Add(1);
            queue.Add(2);
            queue.Add(3);
            queue.Add(4);
            return queue;
        }
    }
}
