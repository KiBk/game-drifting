using System.Collections.Generic;
using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class MultiplayerHud : MonoBehaviour
    {
        public MultiplayerBootstrap bootstrap;
        public MultiplayerCoordinator coordinator;

        private GUIStyle panelStyle;
        private GUIStyle labelStyle;
        private GUIStyle titleStyle;
        private int watchedCarIndex = -1;
        private float smoothedFrameTime = 1f / 60f;

        public bool GameplayPanelVisible { get; private set; } = true;

        public void ToggleGameplayPanel()
        {
            GameplayPanelVisible = !GameplayPanelVisible;
        }

        private void Start()
        {
            if (IsMobileLayoutEnabled())
            {
                MobileControlLayout.RequestLandscapeOrientation();
            }
        }

        private void Update()
        {
            if (Time.unscaledDeltaTime > 0f && Time.unscaledDeltaTime < 1f)
            {
                smoothedFrameTime = Mathf.Lerp(smoothedFrameTime, Time.unscaledDeltaTime, 0.05f);
            }

            if (Input.GetKeyDown(KeyCode.Tab) && !HasLocalDriverCar())
            {
                CycleCar(1);
            }

            RefreshCameraTarget();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (IsMobileLayoutEnabled() && !MobileControlLayout.IsLandscape(Screen.width, Screen.height))
            {
                GUI.depth = -100;
                DrawPortraitGuard();
                return;
            }

            GUI.depth = 0;

            string status = bootstrap == null ? "Starting multiplayer…" : bootstrap.Status;
            int connected = bootstrap == null
                ? coordinator == null ? 0 : coordinator.ConnectedCount
                : bootstrap.ConnectedCount;

            if (bootstrap == null || !bootstrap.IsGameplayReady)
            {
                DrawConnectionPanel(status, connected);
                return;
            }

            if (GameplayPanelVisible)
            {
                DrawGameplayPanel(status, connected);
            }
        }

        private void DrawConnectionPanel(string status, int connected)
        {
            bool mobile = IsMobileLayoutEnabled();
            float panelWidth = mobile ? Mathf.Min(560f, Screen.width - 24f) : 560f;
            bool showInvite = HasHostInvite();
            float panelHeight = showInvite ? 282f : 222f;
            Rect panel = new Rect(
                Screen.width * 0.5f - panelWidth * 0.5f,
                mobile ? 12f : 24f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, string.Empty, panelStyle);
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, panel.height - 24f));
            GUILayout.Label("Convoy Rally Online", titleStyle);
            GUILayout.Label(status, labelStyle);
            GUILayout.Label($"Room: {connected}/{MultiplayerCoordinator.MaximumParticipants} — share an invite link to add drivers", labelStyle);
            GUILayout.Label(GetNetworkQualitySummary(), labelStyle);
            if (GetCars().Count > 0)
            {
                GUILayout.Label(
                    mobile ? "Previewing an active car…" : "Previewing an active car — Tab switches cars",
                    labelStyle);
            }
            else
            {
                GUILayout.Label("Waiting for the first active car…", labelStyle);
            }

            if (showInvite)
            {
                DrawInviteControls();
            }

            if (bootstrap != null && bootstrap.CanCreateFreshRoom && GUILayout.Button("Create a fresh room"))
            {
                bootstrap.CreateFreshRoom();
            }
            else if (bootstrap != null && bootstrap.CanRetry && GUILayout.Button("Reconnect now"))
            {
                bootstrap.RetryNow();
            }

            GUILayout.EndArea();
        }

        private void DrawGameplayPanel(string status, int connected)
        {
            bool mobile = IsMobileLayoutEnabled();
            bool hasInvite = HasHostInvite();
            float scale = MobileControlLayout.GetUiScale(Screen.height);
            float toolbarWidth = mobile
                ? MobileControlLayout.GetToolbarReservedWidth(
                    Screen.height,
                    scale,
                    3)
                : 0f;
            float mobileMargin = Mathf.Clamp(Screen.height * 0.025f, 8f, 18f);
            float panelWidth = mobile
                ? Mathf.Min(620f, Mathf.Max(240f, Screen.width - toolbarWidth - mobileMargin - 8f))
                : 560f;
            bool showInvite = hasInvite;
            float panelHeight = showInvite ? 158f : 108f;
            Rect panel = mobile
                ? new Rect(mobileMargin, 12f, panelWidth, panelHeight)
                : new Rect(
                    Screen.width * 0.5f - panelWidth * 0.5f,
                    Screen.height - panelHeight - 36f,
                    panelWidth,
                    panelHeight);
            GUI.Box(panel, string.Empty, panelStyle);
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 10f, panel.width - 32f, panel.height - 20f));
            GUILayout.Label(status, labelStyle);
            GUILayout.Label(
                $"Drivers online: {connected}/{MultiplayerCoordinator.MaximumParticipants} | {GetNetworkQualitySummary()}",
                labelStyle);
            if (showInvite)
            {
                DrawInviteControls();
            }
            GUILayout.EndArea();
        }

        private bool HasHostInvite()
        {
            return bootstrap != null &&
                   bootstrap.IsSessionHost &&
                   !string.IsNullOrWhiteSpace(bootstrap.InviteUrl);
        }

        private void DrawInviteControls()
        {
            GUILayout.Label($"Invite code: {bootstrap.InviteCode}", labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.TextField(bootstrap.InviteUrl);
            if (GUILayout.Button("Copy link", GUILayout.Width(100f)))
            {
                GUIUtility.systemCopyBuffer = bootstrap.InviteUrl;
            }
            GUILayout.EndHorizontal();
        }

        private string GetNetworkQualitySummary()
        {
            int framesPerSecond = Mathf.RoundToInt(1f / Mathf.Max(smoothedFrameTime, 0.001f));
            if (bootstrap == null || !bootstrap.IsOnlineSession)
            {
                return $"Local | {framesPerSecond} FPS";
            }

            ulong rtt = bootstrap.CurrentRttMilliseconds;
            string latency = rtt == 0 ? "Measuring RTT" : $"{rtt} ms RTT";
            return $"{latency} | {MultiplayerNetworkTuning.TickRate} Hz network | {framesPerSecond} FPS";
        }

        private bool HasLocalDriverCar()
        {
            foreach (NetworkRallyCar car in FindObjectsByType<NetworkRallyCar>())
            {
                if (car.IsSpawned && car.IsOwner)
                {
                    return true;
                }
            }

            return false;
        }

        private void CycleCar(int direction)
        {
            List<Transform> cars = GetCars();
            if (cars.Count == 0)
            {
                watchedCarIndex = -1;
                return;
            }

            ChaseCamera camera = FindAnyObjectByType<ChaseCamera>();
            int currentIndex = camera == null ? -1 : cars.IndexOf(camera.target);
            watchedCarIndex = currentIndex < 0
                ? UnityEngine.Random.Range(0, cars.Count)
                : (currentIndex + direction + cars.Count) % cars.Count;
            SetCameraTarget(cars[watchedCarIndex]);
        }

        private void RefreshCameraTarget()
        {
            ChaseCamera camera = FindAnyObjectByType<ChaseCamera>();
            if (camera == null)
            {
                return;
            }

            if (HasLocalDriverCar())
            {
                foreach (NetworkRallyCar car in FindObjectsByType<NetworkRallyCar>())
                {
                    if (car.IsSpawned && car.IsOwner)
                    {
                        camera.target = car.transform;
                        return;
                    }
                }

                return;
            }

            List<Transform> cars = GetCars();
            if (cars.Count == 0)
            {
                watchedCarIndex = -1;
                return;
            }

            int currentIndex = cars.IndexOf(camera.target);
            if (currentIndex >= 0)
            {
                watchedCarIndex = currentIndex;
                return;
            }

            Transform target = ChooseRandomPreviewCar(cars);
            watchedCarIndex = cars.IndexOf(target);
            camera.target = target;
        }

        private static Transform ChooseRandomPreviewCar(List<Transform> cars)
        {
            List<Transform> movingCars = new List<Transform>();
            foreach (Transform car in cars)
            {
                Rigidbody body = car == null ? null : car.GetComponent<Rigidbody>();
                if (body != null && body.linearVelocity.sqrMagnitude > 1f)
                {
                    movingCars.Add(car);
                }
            }

            List<Transform> candidates = movingCars.Count > 0 ? movingCars : cars;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private List<Transform> GetCars()
        {
            List<NetworkRallyCar> networkCars = new List<NetworkRallyCar>();
            foreach (NetworkRallyCar car in FindObjectsByType<NetworkRallyCar>())
            {
                if (car.IsSpawned)
                {
                    networkCars.Add(car);
                }
            }

            networkCars.Sort((left, right) => left.OwnerClientId.CompareTo(right.OwnerClientId));
            List<Transform> cars = new List<Transform>(networkCars.Count);
            foreach (NetworkRallyCar car in networkCars)
            {
                cars.Add(car.transform);
            }

            return cars;
        }

        private static void SetCameraTarget(Transform target)
        {
            ChaseCamera camera = FindAnyObjectByType<ChaseCamera>();
            if (camera != null)
            {
                camera.target = target;
            }
        }

        private bool IsMobileLayoutEnabled()
        {
            return MobileControlLayout.ShouldEnable(
                Input.touchSupported,
                Application.isMobilePlatform,
                Application.absoluteURL);
        }

        private void DrawPortraitGuard()
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.05f, 0.08f, 0.1f, 0.96f);
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), string.Empty, panelStyle);
            GUI.color = previousColor;

            float panelWidth = Screen.width * 0.82f;
            float panelHeight = Mathf.Max(160f, Screen.height * 0.34f);
            Rect panel = new Rect(
                Screen.width * 0.5f - panelWidth * 0.5f,
                Screen.height * 0.5f - panelHeight * 0.5f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, string.Empty, panelStyle);
            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, panel.height - 36f));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Rotate your phone", titleStyle);
            GUILayout.Label("Convoy Rally supports touch driving in landscape mode.", labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            panelStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = Color.white },
                padding = new RectOffset(14, 14, 12, 12)
            };
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            titleStyle ??= new GUIStyle(labelStyle)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float scale = IsMobileLayoutEnabled()
                ? Mathf.Clamp(Screen.height / 600f, 0.85f, 1.3f)
                : 1f;
            panelStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(16f * scale));
            labelStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(15f * scale));
            titleStyle.fontSize = Mathf.Max(18, Mathf.RoundToInt(20f * scale));
        }
    }
}
