using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class MultiplayerHud : MonoBehaviour
    {
        public MultiplayerBootstrap bootstrap;
        public MultiplayerCoordinator coordinator;

        private GUIStyle panelStyle;
        private GUIStyle labelStyle;
        private int watchedCarIndex;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) && IsSpectator())
            {
                CycleCar(1);
            }

            RefreshSpectatorTarget();
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

            string status = bootstrap == null ? "Starting multiplayer…" : bootstrap.Status;
            int connected = coordinator == null ? 0 : coordinator.ConnectedCount;
            if (connected == 0 && bootstrap != null)
            {
                connected = bootstrap.OfflineConnectedCount;
            }
            if (!TryGetLocalState(out NetworkParticipantState state) || state.role == MultiplayerRole.Driver)
            {
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 170f, Screen.height - 122f, 340f, 72f),
                    $"{status}\nPlayers: {connected}/{MultiplayerCoordinator.MaximumParticipants}",
                    panelStyle);
                return;
            }

            Rect panel = new Rect(Screen.width * 0.5f - 170f, 14f, 340f, 174f);
            GUI.Box(panel, string.Empty, panelStyle);
            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 20f));
            GUILayout.Label("Spectating", labelStyle);
            GUILayout.Label($"Queue position: {state.queuePosition}", labelStyle);
            GUILayout.Label($"Players: {connected}/{MultiplayerCoordinator.MaximumParticipants}", labelStyle);
            GUILayout.Label(status, labelStyle);
            if (GetCars().Count < MultiplayerCoordinator.DriverSlots)
            {
                GUILayout.Label("Waiting for another driver", labelStyle);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous Car"))
            {
                CycleCar(-1);
            }

            if (GUILayout.Button("Next Car"))
            {
                CycleCar(1);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Tab also switches cars", labelStyle);
            GUILayout.EndArea();
        }

        private bool IsSpectator()
        {
            return TryGetLocalState(out NetworkParticipantState state) && state.role == MultiplayerRole.Spectator;
        }

        private bool TryGetLocalState(out NetworkParticipantState state)
        {
            if (coordinator != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                return coordinator.TryGetParticipant(NetworkManager.Singleton.LocalClientId, out state);
            }

            state = default;
            return false;
        }

        private void CycleCar(int direction)
        {
            List<Transform> cars = GetCars();
            if (cars.Count == 0)
            {
                watchedCarIndex = 0;
                return;
            }

            watchedCarIndex = (watchedCarIndex + direction + cars.Count) % cars.Count;
            SetCameraTarget(cars[watchedCarIndex]);
        }

        private void RefreshSpectatorTarget()
        {
            if (!IsSpectator())
            {
                return;
            }

            List<Transform> cars = GetCars();
            if (cars.Count == 0)
            {
                SetCameraTarget(null);
                watchedCarIndex = 0;
                return;
            }

            watchedCarIndex = Mathf.Clamp(watchedCarIndex, 0, cars.Count - 1);
            ChaseCamera camera = FindAnyObjectByType<ChaseCamera>();
            if (camera != null && !cars.Contains(camera.target))
            {
                camera.target = cars[watchedCarIndex];
            }
        }

        private List<Transform> GetCars()
        {
            return coordinator == null ? new List<Transform>() : coordinator.GetActiveCarTransforms();
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
