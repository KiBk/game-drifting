using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace HeavySuvPrototype
{
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class MultiplayerBootstrap : MonoBehaviour
    {
        public const ushort NetworkProtocolVersion = 7;
        public const string PreferredRelayRegion = "europe-north1";
        public const string RelayConnectionType = "wss";
        public const float GameplayReadyTimeoutSeconds = 18f;

        public GameObject carPrefab;
        public string Status { get; private set; } = "Starting multiplayer…";
        public bool IsOnlineSession { get; private set; }
        public bool IsReconnecting { get; private set; }
        public bool IsGameplayReady { get; private set; }
        public bool CanRetry => !InviteExpired && !connecting && !IsGameplayReady && retryRoutine == null;
        public bool CanCreateFreshRoom => InviteExpired && !connecting && !IsGameplayReady && retryRoutine == null;
        public bool InviteExpired { get; private set; }
        public bool IsSessionHost => IsOnlineSession && networkManager != null && networkManager.IsHost;
        public string InviteCode => inviteCode ?? string.Empty;
        public string InviteUrl => string.IsNullOrWhiteSpace(inviteCode)
            ? string.Empty
            : MultiplayerInvite.BuildInviteUrl(Application.absoluteURL, inviteCode);
        public int ConnectedCount => GetConnectedCount();
        public ulong CurrentRttMilliseconds => GetCurrentRttMilliseconds();
        public int OfflineConnectedCount { get; private set; }

        private readonly MultiplayerReadinessGate readiness = new MultiplayerReadinessGate();
        private NetworkManager networkManager;
        private UnityTransport transport;
        private Coroutine retryRoutine;
        private Coroutine gameplayReadyTimeout;
        private bool shuttingDown;
        private bool connecting;
        private bool localCarReady;
        private string inviteCode;
        private string requestedJoinCode;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.ProtocolVersion = NetworkProtocolVersion;
            networkManager.NetworkConfig.TickRate = MultiplayerNetworkTuning.TickRate;
            transport = GetComponent<UnityTransport>();
            transport.UseWebSockets = true;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private async void Start()
        {
            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {
                StartLocalFallback();
                return;
            }

            requestedJoinCode = MultiplayerInvite.ReadJoinCode(Application.absoluteURL);
            await ConnectOnlineAsync();
        }

        public void RetryNow()
        {
            if (shuttingDown || connecting || IsGameplayReady || retryRoutine != null)
            {
                return;
            }

            StopRetryRoutine();
            IsReconnecting = true;
            Status = requestedJoinCode == null ? "Creating a fresh room…" : "Reconnecting to invite…";
            retryRoutine = StartCoroutine(ReconnectAfterDelay(0f, 0f));
        }

        public void CreateFreshRoom()
        {
            if (shuttingDown || connecting || IsGameplayReady || retryRoutine != null)
            {
                return;
            }

            requestedJoinCode = null;
            InviteExpired = false;
            IsReconnecting = true;
            Status = "Creating a fresh room…";
            retryRoutine = StartCoroutine(ReconnectAfterDelay(0f, 0f));
        }

        public void NotifyLocalCarReady()
        {
            localCarReady = true;
            readiness.MarkNetworkReady();
            TryActivateLocalAssignment();
        }

        private async Task ConnectOnlineAsync()
        {
            if (connecting || shuttingDown)
            {
                return;
            }

            connecting = true;
            IsGameplayReady = false;
            Status = requestedJoinCode == null
                ? IsReconnecting ? "Creating a fresh room…" : "Creating a room…"
                : IsReconnecting ? "Reconnecting to invite…" : "Joining invite room…";
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    string profile = $"web{Guid.NewGuid():N}".Substring(0, 15);
                    InitializationOptions initialization = new InitializationOptions().SetProfile(profile);
                    await UnityServices.InitializeAsync(initialization);
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                if (requestedJoinCode == null)
                {
                    await StartRelayHostAsync();
                }
                else
                {
                    await StartRelayClientAsync(requestedJoinCode);
                }
                if (shuttingDown)
                {
                    ShutdownNetwork();
                    return;
                }

                InviteExpired = false;
                IsOnlineSession = true;
                readiness.MarkSessionReady();
                if (networkManager.IsConnectedClient)
                {
                    readiness.MarkNetworkReady();
                }
                Status = readiness.NetworkReady ? "Assigning your car…" : "Synchronizing realtime network…";
                TryActivateLocalAssignment();
                StartGameplayReadyTimeout();
            }
            catch (Exception exception)
            {
                IsOnlineSession = false;
                ShutdownNetwork();
                if (requestedJoinCode != null && MultiplayerInvite.IsExpiredJoinCode(exception))
                {
                    InviteExpired = true;
                    IsReconnecting = false;
                    Status = "Invite expired — create a fresh room";
                    Debug.LogWarning($"Multiplayer invite expired: {exception}");
                    return;
                }

                bool full = exception.Message.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0;
                string status = full ? "Room full — retrying" : "Connection failed — retrying";
                Debug.LogWarning($"Multiplayer connection failed: {exception}");
                ScheduleReconnect(status, 2.5f, 4.5f);
            }
            finally
            {
                connecting = false;
            }
        }

        private async Task StartRelayHostAsync()
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(
                MultiplayerCoordinator.MaximumParticipants - 1,
                PreferredRelayRegion);
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, RelayConnectionType));
            inviteCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            if (!networkManager.StartHost())
            {
                throw new InvalidOperationException("Netcode could not start the Relay host.");
            }
        }

        private async Task StartRelayClientAsync(string joinCode)
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, RelayConnectionType));
            inviteCode = joinCode;
            if (!networkManager.StartClient())
            {
                throw new InvalidOperationException("Netcode could not start the Relay client.");
            }
        }

        private void StartLocalFallback()
        {
            Status = "Local mode — link Unity Cloud";
            IsOnlineSession = false;
            IsGameplayReady = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            HeavySuvVehicleController vehicle = HeavySuvPrototypeFactory.CreateVehicle(Vector3.zero);
            VehicleHud hud = vehicle.gameObject.AddComponent<VehicleHud>();
            hud.Bind(vehicle);
            ChaseCamera camera = FindAnyObjectByType<ChaseCamera>();
            if (camera != null)
            {
                camera.target = vehicle.transform;
            }
            OfflineConnectedCount = 1;
#else
            networkManager.StartHost();
#endif
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId != networkManager.LocalClientId)
            {
                return;
            }

            readiness.MarkNetworkReady();
            Status = readiness.SessionReady ? "Assigning your car…" : "Joining realtime network…";
            TryActivateLocalAssignment();
        }

        private void TryActivateLocalAssignment()
        {
            if (!readiness.IsReady || !IsOnlineSession)
            {
                return;
            }

            Status = "Assigning your car…";
            if (localCarReady)
            {
                IsGameplayReady = true;
                IsReconnecting = false;
                StopGameplayReadyTimeout();
                Status = "Connected — Driving";
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (shuttingDown || !IsOnlineSession || clientId != networkManager.LocalClientId)
            {
                return;
            }

            if (IsReconnecting)
            {
                return;
            }

            IsGameplayReady = false;
            localCarReady = false;
            readiness.Reset();
            StopGameplayReadyTimeout();
            BeginReconnect("Host left — retrying invite");
        }

        private void BeginReconnect(string status)
        {
            if (IsReconnecting)
            {
                return;
            }

            ScheduleReconnect(status, 1.5f, 3.5f);
        }

        private void ScheduleReconnect(string status, float minimumDelay, float maximumDelay)
        {
            IsReconnecting = true;
            IsGameplayReady = false;
            Status = status;
            StopGameplayReadyTimeout();
            StopRetryRoutine();
            retryRoutine = StartCoroutine(ReconnectAfterDelay(minimumDelay, maximumDelay));
        }

        private IEnumerator ReconnectAfterDelay(float minimumDelay, float maximumDelay)
        {
            yield return LeaveCurrentNetwork();
            float delay = minimumDelay >= maximumDelay
                ? minimumDelay
                : UnityEngine.Random.Range(minimumDelay, maximumDelay);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            retryRoutine = null;
            _ = ConnectOnlineAsync();
        }

        private IEnumerator LeaveCurrentNetwork()
        {
            readiness.Reset();
            localCarReady = false;
            IsOnlineSession = false;
            inviteCode = null;
            if (networkManager == null)
            {
                yield break;
            }

            if (networkManager.IsListening && !networkManager.ShutdownInProgress)
            {
                networkManager.Shutdown();
            }

            while (networkManager.ShutdownInProgress)
            {
                yield return null;
            }
        }

        private void StartGameplayReadyTimeout()
        {
            StopGameplayReadyTimeout();
            if (!IsGameplayReady)
            {
                gameplayReadyTimeout = StartCoroutine(WaitForGameplayReady());
            }
        }

        private IEnumerator WaitForGameplayReady()
        {
            float deadline = Time.realtimeSinceStartup + GameplayReadyTimeoutSeconds;
            while (!IsGameplayReady && !IsReconnecting && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            gameplayReadyTimeout = null;
            if (!IsGameplayReady && !IsReconnecting)
            {
                BeginReconnect("Realtime setup timed out — retrying");
            }
        }

        private void StopRetryRoutine()
        {
            if (retryRoutine == null)
            {
                return;
            }

            StopCoroutine(retryRoutine);
            retryRoutine = null;
        }

        private void StopGameplayReadyTimeout()
        {
            if (gameplayReadyTimeout == null)
            {
                return;
            }

            StopCoroutine(gameplayReadyTimeout);
            gameplayReadyTimeout = null;
        }

        private int GetConnectedCount()
        {
            if (networkManager == null || !networkManager.IsListening)
            {
                return OfflineConnectedCount;
            }

            if (networkManager.IsServer)
            {
                return networkManager.ConnectedClientsIds.Count;
            }

            return networkManager.IsConnectedClient ? 2 : 0;
        }

        private void ShutdownNetwork()
        {
            inviteCode = null;
            if (networkManager != null && networkManager.IsListening && !networkManager.ShutdownInProgress)
            {
                networkManager.Shutdown();
            }
        }

        private ulong GetCurrentRttMilliseconds()
        {
            if (transport == null || networkManager == null || !networkManager.IsListening)
            {
                return 0;
            }

            if (!networkManager.IsServer)
            {
                return transport.GetCurrentRtt(NetworkManager.ServerClientId);
            }

            ulong maximumRtt = 0;
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.ServerClientId)
                {
                    continue;
                }

                maximumRtt = Math.Max(maximumRtt, transport.GetCurrentRtt(clientId));
            }

            return maximumRtt;
        }

        private void OnDestroy()
        {
            shuttingDown = true;
            StopRetryRoutine();
            StopGameplayReadyTimeout();
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            ShutdownNetwork();
        }
    }
}
