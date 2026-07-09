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
            panelStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Color.white },
                padding = new RectOffset(14, 14, 12, 12)
            };
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white }
            };
            titleStyle ??= new GUIStyle(labelStyle)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            string status = bootstrap == null ? "Starting multiplayer…" : bootstrap.Status;
            int connected = bootstrap == null
                ? coordinator == null ? 0 : coordinator.ConnectedCount
                : bootstrap.ConnectedCount;

            if (bootstrap == null || !bootstrap.IsGameplayReady)
            {
                DrawConnectionPanel(status, connected);
                return;
            }

            GUI.Box(
                new Rect(Screen.width * 0.5f - 210f, Screen.height - 144f, 420f, 94f),
                $"{status}\nDrivers online: {connected}/{MultiplayerCoordinator.MaximumParticipants}\n{GetNetworkQualitySummary()}",
                panelStyle);
        }

        private void DrawConnectionPanel(string status, int connected)
        {
            const float panelWidth = 420f;
            const float panelHeight = 198f;
            Rect panel = new Rect(Screen.width * 0.5f - panelWidth * 0.5f, 24f, panelWidth, panelHeight);
            GUI.Box(panel, string.Empty, panelStyle);
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, panel.height - 24f));
            GUILayout.Label("Convoy Rally Online", titleStyle);
            GUILayout.Label(status, labelStyle);
            GUILayout.Label($"Room: {connected}/{MultiplayerCoordinator.MaximumParticipants} — every slot gets a car", labelStyle);
            GUILayout.Label(GetNetworkQualitySummary(), labelStyle);
            if (GetCars().Count > 0)
            {
                GUILayout.Label("Previewing an active car — Tab switches cars", labelStyle);
            }
            else
            {
                GUILayout.Label("Waiting for the first active car…", labelStyle);
            }

            if (bootstrap != null && bootstrap.CanRetry && GUILayout.Button("Reconnect now"))
            {
                bootstrap.RetryNow();
            }

            GUILayout.EndArea();
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
    }
}
