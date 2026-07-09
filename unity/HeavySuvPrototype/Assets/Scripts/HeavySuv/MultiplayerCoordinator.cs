using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class MultiplayerCoordinator : MonoBehaviour
    {
        public const int MaximumParticipants = 8;
        public const int DriverSlots = MaximumParticipants;
        public const int NetworkVehicleLayer = 8;

        public static readonly Color32[] VehiclePalette =
        {
            new Color32(235, 64, 52, 255),
            new Color32(46, 134, 222, 255),
            new Color32(252, 186, 3, 255),
            new Color32(46, 204, 113, 255),
            new Color32(155, 89, 182, 255),
            new Color32(255, 126, 38, 255),
            new Color32(26, 188, 156, 255),
            new Color32(236, 240, 241, 255)
        };

        private static readonly float[] SpawnLanePositions = { -1.35f, 1.35f, -4.05f, 4.05f };

        public GameObject carPrefab;

        private readonly DriverQueue queue = new DriverQueue(DriverSlots, MaximumParticipants);
        private readonly List<ulong> connectionOrder = new List<ulong>();
        private readonly Dictionary<ulong, NetworkObject> cars = new Dictionary<ulong, NetworkObject>();
        private readonly Dictionary<ulong, uint> assignedColors = new Dictionary<ulong, uint>();
        private NetworkManager networkManager;

        public int ConnectedCount => networkManager != null && networkManager.IsServer
            ? networkManager.ConnectedClientsIds.Count
            : 0;

        private void Awake()
        {
            Physics.IgnoreLayerCollision(NetworkVehicleLayer, NetworkVehicleLayer, true);
            AttachNetworkManager();
        }

        private void Start()
        {
            AttachNetworkManager();
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                OnClientConnected(clientId);
            }
        }

        private void AttachNetworkManager()
        {
            if (networkManager != null)
            {
                return;
            }

            networkManager = FindAnyObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnServerStopped += OnServerStopped;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager == null ||
                !networkManager.IsServer ||
                connectionOrder.Contains(clientId) ||
                !queue.Add(clientId))
            {
                return;
            }

            connectionOrder.Add(clientId);
            if (queue.GetRole(clientId) == MultiplayerRole.Driver)
            {
                SpawnCar(clientId, queue.GetDriverSlot(clientId));
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (networkManager == null || !networkManager.IsServer || !connectionOrder.Remove(clientId))
            {
                return;
            }

            DespawnCar(clientId);
            ulong? promoted = queue.Remove(clientId);
            if (promoted.HasValue)
            {
                SpawnCar(promoted.Value, queue.GetDriverSlot(promoted.Value));
            }
        }

        private void OnServerStopped(bool wasHost)
        {
            queue.Clear();
            connectionOrder.Clear();
            cars.Clear();
            assignedColors.Clear();
        }

        private void SpawnCar(ulong clientId, int driverSlot)
        {
            if (carPrefab == null || cars.ContainsKey(clientId))
            {
                return;
            }

            Vector3 spawnPosition = GetSpawnPosition(driverSlot);
            GameObject carObject = Instantiate(carPrefab, spawnPosition, Quaternion.identity);
            SetLayerRecursively(carObject, NetworkVehicleLayer);
            HeavySuvVehicleController controller = carObject.GetComponent<HeavySuvVehicleController>();
            if (controller != null)
            {
                controller.SetRespawnPose(spawnPosition, Quaternion.identity);
            }

            NetworkObject networkObject = carObject.GetComponent<NetworkObject>();
            networkObject.SpawnWithOwnership(clientId, true);
            cars[clientId] = networkObject;

            NetworkRallyCar rallyCar = carObject.GetComponent<NetworkRallyCar>();
            if (rallyCar != null)
            {
                Color32 color = ChooseColor();
                assignedColors[clientId] = NetworkRallyCar.PackColor(color);
                rallyCar.SetColor(color);
            }
        }

        private void DespawnCar(ulong clientId)
        {
            assignedColors.Remove(clientId);
            if (!cars.Remove(clientId, out NetworkObject networkObject) || networkObject == null)
            {
                return;
            }

            if (networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
            }
        }

        private Color32 ChooseColor()
        {
            HashSet<uint> used = new HashSet<uint>();
            foreach (uint assignedColor in assignedColors.Values)
            {
                used.Add(assignedColor);
            }

            int start = UnityEngine.Random.Range(0, VehiclePalette.Length);
            for (int offset = 0; offset < VehiclePalette.Length; offset += 1)
            {
                Color32 candidate = VehiclePalette[(start + offset) % VehiclePalette.Length];
                if (!used.Contains(NetworkRallyCar.PackColor(candidate)))
                {
                    return candidate;
                }
            }

            return VehiclePalette[start];
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public static Vector3 GetSpawnPosition(int driverSlot)
        {
            int clampedSlot = Mathf.Clamp(driverSlot, 0, MaximumParticipants - 1);
            int row = clampedSlot / 4;
            int column = clampedSlot % 4;
            return new Vector3(SpawnLanePositions[column], 0.52f, row * -4.5f);
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                networkManager.OnServerStopped -= OnServerStopped;
            }

            queue.Clear();
            connectionOrder.Clear();
            cars.Clear();
            assignedColors.Clear();
        }
    }
}
