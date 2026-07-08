using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace HeavySuvPrototype
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MultiplayerCoordinator : NetworkBehaviour
    {
        public const int MaximumParticipants = 8;
        public const int DriverSlots = 2;
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

        public GameObject carPrefab;

        private readonly DriverQueue queue = new DriverQueue(DriverSlots, MaximumParticipants);
        private readonly List<ulong> connectionOrder = new List<ulong>();
        private readonly Dictionary<ulong, NetworkObject> cars = new Dictionary<ulong, NetworkObject>();
        private NetworkList<NetworkParticipantState> participants;
        private ISession distributedSession;
        private NetworkObject distributedLocalCar;
        private int distributedLocalRank = -1;

        public NetworkList<NetworkParticipantState> Participants => participants;
        public int ConnectedCount => distributedSession == null
            ? participants == null ? 0 : participants.Count
            : distributedSession.Players.Count;

        private void Awake()
        {
            participants = new NetworkList<NetworkParticipantState>();
            Physics.IgnoreLayerCollision(NetworkVehicleLayer, NetworkVehicleLayer, true);
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.DistributedAuthorityMode)
            {
                EvaluateDistributedRole();
                return;
            }

            if (!IsServer)
            {
                return;
            }

            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                OnClientConnected(clientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            BindSession(null);
            if (NetworkManager != null && IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            queue.Clear();
            connectionOrder.Clear();
            cars.Clear();
        }

        private void Update()
        {
            if (distributedSession != null && distributedLocalCar == null)
            {
                EvaluateDistributedRole();
            }
        }

        public void BindSession(ISession session)
        {
            if (distributedSession != null)
            {
                distributedSession.Changed -= OnDistributedSessionChanged;
                distributedSession.PlayerJoined -= OnDistributedPlayerChanged;
                distributedSession.PlayerHasLeft -= OnDistributedPlayerChanged;
            }

            distributedSession = session;
            distributedLocalRank = -1;
            if (distributedSession != null)
            {
                distributedSession.Changed += OnDistributedSessionChanged;
                distributedSession.PlayerJoined += OnDistributedPlayerChanged;
                distributedSession.PlayerHasLeft += OnDistributedPlayerChanged;
                EvaluateDistributedRole();
            }
        }

        public bool TryGetParticipant(ulong clientId, out NetworkParticipantState state)
        {
            if (distributedSession != null &&
                NetworkManager.Singleton != null &&
                clientId == NetworkManager.Singleton.LocalClientId)
            {
                int rank = GetDistributedLocalRank();
                bool driver = rank >= 0 && rank < DriverSlots;
                state = new NetworkParticipantState
                {
                    clientId = clientId,
                    role = driver ? MultiplayerRole.Driver : MultiplayerRole.Spectator,
                    queuePosition = driver ? 0 : Mathf.Max(1, rank - DriverSlots + 1),
                    driverSlot = driver ? rank : -1,
                    car = distributedLocalCar == null ? default : distributedLocalCar
                };
                return rank >= 0;
            }

            if (participants != null)
            {
                for (int index = 0; index < participants.Count; index += 1)
                {
                    if (participants[index].clientId == clientId)
                    {
                        state = participants[index];
                        return true;
                    }
                }
            }

            state = default;
            return false;
        }

        public List<Transform> GetActiveCarTransforms()
        {
            if (distributedSession != null)
            {
                return FindObjectsByType<NetworkRallyCar>(FindObjectsSortMode.None)
                    .Where(car => car.NetworkObject != null && car.NetworkObject.IsSpawned)
                    .OrderBy(car => car.OwnerClientId)
                    .Select(car => car.transform)
                    .ToList();
            }

            List<(int slot, Transform transform)> sorted = new List<(int, Transform)>();
            if (participants != null)
            {
                for (int index = 0; index < participants.Count; index += 1)
                {
                    NetworkParticipantState state = participants[index];
                    if (state.role == MultiplayerRole.Driver &&
                        state.car.TryGet(out NetworkObject networkObject) &&
                        networkObject != null)
                    {
                        sorted.Add((state.driverSlot, networkObject.transform));
                    }
                }
            }

            sorted.Sort((left, right) => left.slot.CompareTo(right.slot));
            List<Transform> result = new List<Transform>();
            foreach ((int slot, Transform transform) item in sorted)
            {
                result.Add(item.transform);
            }

            return result;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer || connectionOrder.Contains(clientId) || !queue.Add(clientId))
            {
                return;
            }

            connectionOrder.Add(clientId);
            if (queue.GetRole(clientId) == MultiplayerRole.Driver)
            {
                SpawnCar(clientId, queue.GetDriverSlot(clientId));
            }

            RebuildParticipantStates();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer || !connectionOrder.Remove(clientId))
            {
                return;
            }

            DespawnCar(clientId);
            ulong? promoted = queue.Remove(clientId);
            if (promoted.HasValue)
            {
                SpawnCar(promoted.Value, queue.GetDriverSlot(promoted.Value));
            }

            RebuildParticipantStates();
        }

        private void SpawnCar(ulong clientId, int driverSlot)
        {
            if (carPrefab == null || cars.ContainsKey(clientId))
            {
                return;
            }

            Vector3 spawnPosition = new Vector3(driverSlot == 0 ? -1.35f : 1.35f, 0.52f, 0f);
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
                rallyCar.SetColor(ChooseColor());
            }
        }

        private void DespawnCar(ulong clientId)
        {
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
            foreach (NetworkObject networkObject in cars.Values)
            {
                NetworkRallyCar car = networkObject == null ? null : networkObject.GetComponent<NetworkRallyCar>();
                if (car != null)
                {
                    used.Add(NetworkRallyCar.PackColor(car.VehicleColor));
                }
            }

            foreach (NetworkRallyCar existing in FindObjectsByType<NetworkRallyCar>(FindObjectsSortMode.None))
            {
                used.Add(NetworkRallyCar.PackColor(existing.VehicleColor));
            }

            int start = Random.Range(0, VehiclePalette.Length);
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

        private void RebuildParticipantStates()
        {
            participants.Clear();
            foreach (ulong clientId in connectionOrder)
            {
                MultiplayerRole role = queue.GetRole(clientId);
                NetworkObjectReference carReference = default;
                if (cars.TryGetValue(clientId, out NetworkObject car) && car != null)
                {
                    carReference = car;
                }

                participants.Add(new NetworkParticipantState
                {
                    clientId = clientId,
                    role = role,
                    queuePosition = queue.GetQueuePosition(clientId),
                    driverSlot = queue.GetDriverSlot(clientId),
                    car = carReference
                });
            }
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void OnDistributedSessionChanged()
        {
            EvaluateDistributedRole();
        }

        private void OnDistributedPlayerChanged(string playerId)
        {
            EvaluateDistributedRole();
        }

        private int GetDistributedLocalRank()
        {
            if (distributedSession?.CurrentPlayer == null)
            {
                return -1;
            }

            List<IReadOnlyPlayer> orderedPlayers = distributedSession.Players
                .OrderBy(player => player.Joined)
                .ThenBy(player => player.Id)
                .ToList();
            return orderedPlayers.FindIndex(player => player.Id == distributedSession.CurrentPlayer.Id);
        }

        private void EvaluateDistributedRole()
        {
            if (distributedSession == null ||
                NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsConnectedClient)
            {
                return;
            }

            int rank = GetDistributedLocalRank();
            if (rank < 0)
            {
                return;
            }

            distributedLocalRank = rank;
            if (rank < DriverSlots)
            {
                SpawnDistributedLocalCar(rank);
            }
            else
            {
                DespawnDistributedLocalCar();
            }
        }

        private void SpawnDistributedLocalCar(int driverSlot)
        {
            if (distributedLocalCar != null || carPrefab == null)
            {
                return;
            }

            Vector3 spawnPosition = new Vector3(driverSlot == 0 ? -1.35f : 1.35f, 0.52f, 0f);
            GameObject carObject = Instantiate(carPrefab, spawnPosition, Quaternion.identity);
            SetLayerRecursively(carObject, NetworkVehicleLayer);
            HeavySuvVehicleController controller = carObject.GetComponent<HeavySuvVehicleController>();
            if (controller != null)
            {
                controller.SetRespawnPose(spawnPosition, Quaternion.identity);
            }

            distributedLocalCar = carObject.GetComponent<NetworkObject>();
            distributedLocalCar.Spawn(true);
            NetworkRallyCar rallyCar = carObject.GetComponent<NetworkRallyCar>();
            if (rallyCar != null)
            {
                rallyCar.SetColor(ChooseColor());
            }
        }

        private void DespawnDistributedLocalCar()
        {
            if (distributedLocalCar == null)
            {
                return;
            }

            if (distributedLocalCar.IsSpawned && distributedLocalCar.IsOwner)
            {
                distributedLocalCar.Despawn(true);
            }

            distributedLocalCar = null;
        }
    }
}
