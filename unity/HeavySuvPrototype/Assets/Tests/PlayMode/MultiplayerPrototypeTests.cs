using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeavySuvPrototype.Tests
{
    public sealed class MultiplayerPrototypeTests
    {
        [Test]
        public void AllEightParticipantsDriveAndNinthIsRejected()
        {
            DriverQueue queue = new DriverQueue(
                MultiplayerCoordinator.DriverSlots,
                MultiplayerCoordinator.MaximumParticipants);
            for (ulong clientId = 1; clientId <= 8; clientId += 1)
            {
                Assert.IsTrue(queue.Add(clientId));
                Assert.AreEqual(MultiplayerRole.Driver, queue.GetRole(clientId));
                Assert.AreEqual((int)clientId - 1, queue.GetDriverSlot(clientId));
                Assert.AreEqual(0, queue.GetQueuePosition(clientId));
            }

            Assert.AreEqual(8, MultiplayerCoordinator.DriverSlots);
            Assert.IsFalse(queue.Add(9));
        }

        [Test]
        public void DriverDeparturePromotesFirstSpectatorIntoSameSlot()
        {
            DriverQueue queue = CreateFourParticipantQueueWithTwoDrivers();

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
            DriverQueue queue = CreateFourParticipantQueueWithTwoDrivers();

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
        public void SessionAndNetworkMustBothBeReadyBeforeAssignment()
        {
            MultiplayerReadinessGate gate = new MultiplayerReadinessGate();

            gate.MarkSessionReady();
            Assert.IsFalse(gate.IsReady);
            gate.MarkNetworkReady();
            Assert.IsTrue(gate.IsReady);

            gate.Reset();
            gate.MarkNetworkReady();
            Assert.IsFalse(gate.IsReady);
            gate.MarkSessionReady();
            Assert.IsTrue(gate.IsReady);
        }

        [Test]
        public void EightDriverStartsAreDistinctAndProtocolUsesInviteRooms()
        {
            HashSet<Vector3> positions = new HashSet<Vector3>();
            for (int slot = 0; slot < MultiplayerCoordinator.DriverSlots; slot += 1)
            {
                Assert.IsTrue(positions.Add(MultiplayerCoordinator.GetSpawnPosition(slot)));
            }

            Assert.AreEqual(8, positions.Count);
            Assert.AreEqual(7, MultiplayerBootstrap.NetworkProtocolVersion);
            Assert.AreEqual("join", MultiplayerInvite.QueryParameter);
            Assert.AreEqual("europe-north1", MultiplayerBootstrap.PreferredRelayRegion);
            Assert.AreEqual("wss", MultiplayerBootstrap.RelayConnectionType);
            Assert.AreEqual(7, MultiplayerCoordinator.MaximumParticipants - 1);
            Assert.AreEqual(50u, MultiplayerNetworkTuning.TickRate);
        }

        [Test]
        public void InviteUrlRoundTripsAndReplacesExpiredCode()
        {
            const string sourceUrl = "https://drifting.chronos.kibk.net/?quality=high&join=OLD123#ignored";

            string inviteUrl = MultiplayerInvite.BuildInviteUrl(sourceUrl, " new789 ");

            Assert.AreEqual(
                "https://drifting.chronos.kibk.net/?quality=high&join=NEW789",
                inviteUrl);
            Assert.AreEqual("NEW789", MultiplayerInvite.ReadJoinCode(inviteUrl));
        }

        [Test]
        public void MissingOrEmptyInviteDoesNotJoinOldRoom()
        {
            Assert.IsNull(MultiplayerInvite.ReadJoinCode("https://drifting.chronos.kibk.net/"));
            Assert.IsNull(MultiplayerInvite.ReadJoinCode("https://drifting.chronos.kibk.net/?join="));
            Assert.AreEqual("?join=ABC789", MultiplayerInvite.BuildInviteUrl(string.Empty, "abc789"));
        }

        [Test]
        public void ExpiredRelayJoinCodeIsDetectedThroughWrappedException()
        {
            System.Exception wrapped = new System.InvalidOperationException(
                "Session setup failed",
                new System.Exception("Relay 15009: join code not found"));

            Assert.IsTrue(MultiplayerInvite.IsExpiredJoinCode(wrapped));
            Assert.IsFalse(MultiplayerInvite.IsExpiredJoinCode(new System.Exception("Network timeout")));
        }

        [Test]
        public void MobileBoostAppliesThrottleWhilePressed()
        {
            MobileControlRects controls = MobileControlLayout.Calculate(844f, 390f);
            VehicleInputState input = VehicleInputState.None;

            MobileControlLayout.ApplyPointer(ref input, controls, controls.SteerLeft.center);
            MobileControlLayout.ApplyPointer(ref input, controls, controls.Boost.center);

            Assert.IsTrue(input.steerLeft);
            Assert.IsTrue(input.throttle);
            Assert.IsTrue(input.turbo);
            Assert.IsFalse(input.steerRight);
            Assert.IsFalse(input.brake);
            Assert.IsFalse(controls.SteerRight.Overlaps(controls.Brake));
            Assert.IsFalse(controls.Brake.Overlaps(controls.Throttle));
            Assert.IsFalse(controls.Boost.Overlaps(controls.Throttle));
        }

        [Test]
        public void TurboInputDoesNotImplicitlyApplyThrottle()
        {
            VehicleInputState keyboard = new VehicleInputState { turbo = true };

            VehicleInputState merged = VehicleInputState.Merge(keyboard, VehicleInputState.None);

            Assert.IsTrue(merged.turbo);
            Assert.IsFalse(merged.throttle);
        }

        [Test]
        public void MobileLayoutRequiresLandscapeAndDetectsPhoneOrPreviewMode()
        {
            Assert.IsTrue(MobileControlLayout.IsLandscape(844, 390));
            Assert.IsFalse(MobileControlLayout.IsLandscape(390, 844));
            Assert.IsTrue(MobileControlLayout.ShouldEnable(true, false, string.Empty));
            Assert.IsTrue(MobileControlLayout.ShouldEnable(false, true, string.Empty));
            Assert.IsTrue(MobileControlLayout.ShouldEnable(
                false,
                false,
                "https://drifting.chronos.kibk.net/?touchControls=1"));
            Assert.IsFalse(MobileControlLayout.ShouldEnable(
                false,
                false,
                "https://drifting.chronos.kibk.net/"));
            Assert.IsTrue(MobileControlLayout.ShouldShowControlsByDefault(
                false,
                844f,
                390f,
                string.Empty));
            Assert.IsFalse(MobileControlLayout.ShouldShowControlsByDefault(
                false,
                960f,
                600f,
                string.Empty));
            Assert.IsTrue(MobileControlLayout.ShouldShowControlsByDefault(
                false,
                960f,
                600f,
                "https://drifting.chronos.kibk.net/?touchControls=1"));
        }

        [Test]
        public void MobileToolbarButtonsDoNotOverlap()
        {
            float scale = MobileControlLayout.GetUiScale(390f);
            Rect car = MobileControlLayout.GetToolbarButtonRect(844f, 390f, scale, 0);
            Rect controls = MobileControlLayout.GetToolbarButtonRect(844f, 390f, scale, 1);
            Rect link = MobileControlLayout.GetToolbarButtonRect(844f, 390f, scale, 2);

            Assert.IsFalse(car.Overlaps(controls));
            Assert.IsFalse(controls.Overlaps(link));
            Assert.GreaterOrEqual(link.x, 0f);
        }

        [Test]
        public void TouchInputMergesWithKeyboardInput()
        {
            VehicleInputState keyboard = new VehicleInputState { steerRight = true };
            VehicleInputState touch = new VehicleInputState { throttle = true, turbo = true };

            VehicleInputState merged = VehicleInputState.Merge(keyboard, touch);

            Assert.IsTrue(merged.steerRight);
            Assert.IsTrue(merged.throttle);
            Assert.IsTrue(merged.turbo);
            Assert.IsFalse(merged.brake);
        }

        [Test]
        public void NetworkCarPrefabUsesOwnerAuthorityAndGhostLayer()
        {
            GameObject prefab = Resources.Load<GameObject>("Network/NetworkRallyCar");
            GameObject coordinatorObject = new GameObject("Coordinator Test");
            coordinatorObject.AddComponent<MultiplayerCoordinator>();

            Assert.NotNull(prefab);
            Assert.NotNull(prefab.GetComponent<NetworkObject>());
            Assert.NotNull(prefab.GetComponent<NetworkRigidbody>());
            Assert.NotNull(prefab.GetComponent<NetworkRallyCar>());
            Assert.AreEqual(MultiplayerCoordinator.NetworkVehicleLayer, prefab.layer);
            NetworkTransform networkTransform = prefab.GetComponent<NetworkTransform>();
            Assert.AreEqual(
                NetworkTransform.AuthorityModes.Owner,
                networkTransform.AuthorityMode);
            Assert.AreEqual(NetworkTransform.InterpolationTypes.Lerp, networkTransform.PositionInterpolationType);
            Assert.AreEqual(NetworkTransform.InterpolationTypes.Lerp, networkTransform.RotationInterpolationType);
            Assert.IsTrue(networkTransform.Interpolate);
            Assert.IsTrue(networkTransform.UseUnreliableDeltas);
            Assert.IsFalse(networkTransform.SyncScaleX);
            Assert.IsFalse(networkTransform.SyncScaleY);
            Assert.IsFalse(networkTransform.SyncScaleZ);
            Assert.AreEqual(MultiplayerNetworkTuning.PositionThreshold, networkTransform.PositionThreshold, 0.0001f);
            Assert.AreEqual(MultiplayerNetworkTuning.RotationThreshold, networkTransform.RotAngleThreshold, 0.0001f);
            Assert.IsTrue(networkTransform.UseQuaternionSynchronization);
            Assert.IsTrue(networkTransform.UseQuaternionCompression);
            Assert.IsTrue(networkTransform.UseHalfFloatPrecision);
            Assert.AreEqual(
                NetworkVariableWritePermission.Owner,
                prefab.GetComponent<NetworkRallyCar>().ColorWritePermission);
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(
                MultiplayerCoordinator.NetworkVehicleLayer,
                MultiplayerCoordinator.NetworkVehicleLayer));

            Object.DestroyImmediate(coordinatorObject);
        }

        [Test]
        public void ProjectAndGeneratedMaterialsUseUrp()
        {
            Assert.NotNull(GraphicsSettings.currentRenderPipeline);
            Assert.AreEqual(
                "UniversalRenderPipelineAsset",
                GraphicsSettings.currentRenderPipeline.GetType().Name);

            HeavySuvVehicleController vehicle = HeavySuvPrototypeFactory.CreateVehicle(Vector3.zero);
            try
            {
                foreach (Renderer renderer in vehicle.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.NotNull(material);
                        Assert.NotNull(material.shader);
                        StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
                    }
                }

                foreach (TrailRenderer trail in vehicle.GetComponentsInChildren<TrailRenderer>(true))
                {
                    Assert.NotNull(trail.sharedMaterial);
                    Assert.AreEqual(PrototypeMaterialFactory.LitShaderName, trail.sharedMaterial.shader.name);
                    Assert.AreEqual((int)RenderQueue.Transparent, trail.sharedMaterial.renderQueue);
                }
            }
            finally
            {
                Object.DestroyImmediate(vehicle.gameObject);
            }
        }

        private static DriverQueue CreateFourParticipantQueueWithTwoDrivers()
        {
            DriverQueue queue = new DriverQueue(2, 8);
            queue.Add(1);
            queue.Add(2);
            queue.Add(3);
            queue.Add(4);
            return queue;
        }
    }
}
