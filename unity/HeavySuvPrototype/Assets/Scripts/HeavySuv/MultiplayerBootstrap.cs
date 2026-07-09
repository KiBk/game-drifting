using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace HeavySuvPrototype
{
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class MultiplayerBootstrap : MonoBehaviour
    {
        public const ushort NetworkProtocolVersion = 6;
        public const string PublicSessionId = "convoy-rally-public-v6";
        public const float GameplayReadyTimeoutSeconds = 18f;
        public const float HostMigrationTimeoutSeconds = 55f;

        public GameObject carPrefab;
        public string Status { get; private set; } = "Starting multiplayer…";
        public bool IsOnlineSession { get; private set; }
        public bool IsReconnecting { get; private set; }
        public bool IsGameplayReady { get; private set; }
        public bool CanRetry => !connecting && !IsGameplayReady && retryRoutine == null;
        public int ConnectedCount => session == null ? OfflineConnectedCount : session.Players.Count;
        public int OfflineConnectedCount { get; private set; }

        private readonly MultiplayerReadinessGate readiness = new MultiplayerReadinessGate();
        private NetworkManager networkManager;
        private ISession session;
        private Coroutine retryRoutine;
        private Coroutine gameplayReadyTimeout;
        private Coroutine hostMigrationTimeout;
        private bool shuttingDown;
        private bool connecting;
        private bool localCarReady;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.ProtocolVersion = NetworkProtocolVersion;
            UnityTransport transport = GetComponent<UnityTransport>();
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
            Status = "Reconnecting…";
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
            Status = IsReconnecting ? "Reconnecting to public room…" : "Connecting to public room…";
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

                SessionOptions options = new SessionOptions
                {
                    Name = "Convoy Rally Public Prototype",
                    MaxPlayers = MultiplayerCoordinator.MaximumParticipants,
                    IsPrivate = false
                }
                .WithRelayNetwork()
                .WithHostMigration(new ResetOnlyMigrationDataHandler())
                .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.WSS });

                session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(PublicSessionId, options);
                if (shuttingDown)
                {
                    await session.LeaveAsync();
                    session = null;
                    return;
                }

                session.Deleted += OnSessionLost;
                session.RemovedFromSession += OnSessionLost;
                session.SessionMigrated += OnSessionMigrated;
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
            if (!readiness.IsReady || session == null)
            {
                return;
            }

            Status = "Assigning your car…";
            if (localCarReady)
            {
                IsGameplayReady = true;
                IsReconnecting = false;
                StopGameplayReadyTimeout();
                StopHostMigrationTimeout();
                Status = "Connected — Driving";
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (shuttingDown || !IsOnlineSession || clientId != networkManager.LocalClientId)
            {
                return;
            }

            if (IsReconnecting || hostMigrationTimeout != null)
            {
                return;
            }

            IsGameplayReady = false;
            localCarReady = false;
            readiness.Reset();
            readiness.MarkSessionReady();
            Status = "Host left — migrating room…";
            StopGameplayReadyTimeout();
            hostMigrationTimeout = StartCoroutine(WaitForHostMigration());
        }

        private void OnSessionMigrated()
        {
            if (!shuttingDown)
            {
                Status = IsGameplayReady ? "Connected — Driving" : "New host ready — restoring cars…";
            }
        }

        private void OnSessionLost()
        {
            if (!shuttingDown)
            {
                BeginReconnect("Session lost — retrying");
            }
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
            StopHostMigrationTimeout();
            StopRetryRoutine();
            retryRoutine = StartCoroutine(ReconnectAfterDelay(minimumDelay, maximumDelay));
        }

        private IEnumerator ReconnectAfterDelay(float minimumDelay, float maximumDelay)
        {
            yield return LeaveCurrentSession();
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

        private IEnumerator LeaveCurrentSession()
        {
            readiness.Reset();
            localCarReady = false;

            ISession currentSession = session;
            session = null;
            IsOnlineSession = false;
            if (currentSession == null)
            {
                yield break;
            }

            currentSession.Deleted -= OnSessionLost;
            currentSession.RemovedFromSession -= OnSessionLost;
            currentSession.SessionMigrated -= OnSessionMigrated;
            Task leaveTask = currentSession.LeaveAsync();
            while (!leaveTask.IsCompleted)
            {
                yield return null;
            }

            if (leaveTask.IsFaulted)
            {
                Debug.LogWarning($"Leaving multiplayer session failed: {leaveTask.Exception}");
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

        private IEnumerator WaitForHostMigration()
        {
            float deadline = Time.realtimeSinceStartup + HostMigrationTimeoutSeconds;
            while (!IsGameplayReady && !IsReconnecting && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            hostMigrationTimeout = null;
            if (!IsGameplayReady && !IsReconnecting)
            {
                BeginReconnect("Host migration timed out — retrying");
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

        private void StopHostMigrationTimeout()
        {
            if (hostMigrationTimeout == null)
            {
                return;
            }

            StopCoroutine(hostMigrationTimeout);
            hostMigrationTimeout = null;
        }

        private async void OnDestroy()
        {
            shuttingDown = true;
            StopRetryRoutine();
            StopGameplayReadyTimeout();
            StopHostMigrationTimeout();
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            if (session != null)
            {
                session.Deleted -= OnSessionLost;
                session.RemovedFromSession -= OnSessionLost;
                session.SessionMigrated -= OnSessionMigrated;
                try
                {
                    await session.LeaveAsync();
                }
                catch
                {
                }
            }
        }
    }
}
