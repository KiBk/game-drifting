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
        public const string PublicSessionId = "convoy-rally-public-v1";

        public GameObject carPrefab;
        public string Status { get; private set; } = "Starting multiplayer…";
        public bool IsOnlineSession { get; private set; }
        public bool IsReconnecting { get; private set; }
        public int OfflineConnectedCount { get; private set; }

        private NetworkManager networkManager;
        private ISession session;
        private bool shuttingDown;
        private bool connecting;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            networkManager.NetworkConfig.EnableSceneManagement = false;
            UnityTransport transport = GetComponent<UnityTransport>();
            transport.UseWebSockets = true;
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

        private async Task ConnectOnlineAsync()
        {
            if (connecting || shuttingDown)
            {
                return;
            }

            connecting = true;
            Status = IsReconnecting ? "Reconnecting…" : "Connecting…";
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
                .WithDistributedAuthorityNetwork()
                .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.WSS });

                session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(PublicSessionId, options);
                session.Deleted += OnSessionLost;
                session.RemovedFromSession += OnSessionLost;
                IsOnlineSession = true;
                IsReconnecting = false;
                Status = "Connected";
                FindAnyObjectByType<MultiplayerCoordinator>()?.BindSession(session);
            }
            catch (Exception exception)
            {
                IsOnlineSession = false;
                bool full = exception.Message.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0;
                Status = full ? "Session full — retrying" : $"Connection failed — retrying";
                Debug.LogWarning($"Multiplayer connection failed: {exception}");
                StartCoroutine(RetryConnection());
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

        private void OnClientDisconnected(ulong clientId)
        {
            if (shuttingDown || !IsOnlineSession || clientId != networkManager.LocalClientId)
            {
                return;
            }

            BeginReconnect();
        }

        private void OnSessionLost()
        {
            if (!shuttingDown)
            {
                BeginReconnect();
            }
        }

        private void BeginReconnect()
        {
            if (IsReconnecting)
            {
                return;
            }

            IsReconnecting = true;
            Status = "Reconnecting…";
            StartCoroutine(ReconnectAfterDelay());
        }

        private IEnumerator RetryConnection()
        {
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(2.5f, 4.5f));
            _ = ConnectOnlineAsync();
        }

        private IEnumerator ReconnectAfterDelay()
        {
            yield return LeaveCurrentSession();
            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(1.5f, 3.5f));
            _ = ConnectOnlineAsync();
        }

        private IEnumerator LeaveCurrentSession()
        {
            if (session == null)
            {
                yield break;
            }

            Task leaveTask = session.LeaveAsync();
            while (!leaveTask.IsCompleted)
            {
                yield return null;
            }

            session = null;
            FindAnyObjectByType<MultiplayerCoordinator>()?.BindSession(null);
        }

        private async void OnDestroy()
        {
            shuttingDown = true;
            if (networkManager != null)
            {
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            if (session != null)
            {
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
