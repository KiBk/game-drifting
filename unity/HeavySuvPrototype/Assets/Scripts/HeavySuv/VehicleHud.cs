using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class VehicleHud : MonoBehaviour
    {
        [SerializeField] private HeavySuvVehicleController controller;
        private GUIStyle controlStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle labelStyle;

        public void Bind(HeavySuvVehicleController vehicleController)
        {
            controller = vehicleController;
        }

        private void OnGUI()
        {
            if (controller == null)
            {
                return;
            }

            controlStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 15,
                normal = { textColor = Color.white },
                padding = new RectOffset(14, 14, 12, 12)
            };
            buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fixedHeight = 30f
            };
            selectedButtonStyle ??= new GUIStyle(buttonStyle)
            {
                normal = { textColor = Color.yellow },
                fontStyle = FontStyle.Bold
            };
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white }
            };

            VehicleTelemetrySample telemetry = controller.CaptureTelemetry();
            DrawControlPanel(telemetry);
        }

        private void DrawControlPanel(VehicleTelemetrySample telemetry)
        {
            const float panelWidth = 292f;
            Rect panel = new Rect(Screen.width - panelWidth - 16f, 16f, panelWidth, 318f);
            GUI.Box(panel, string.Empty, controlStyle);

            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 12f, panel.width - 28f, panel.height - 24f));
            GUILayout.Label("Drive selector", labelStyle);
            GUILayout.BeginHorizontal();
            DrawSelectorButton("R", DriveSelectorMode.Reverse);
            DrawSelectorButton("N", DriveSelectorMode.Neutral);
            DrawSelectorButton("D", DriveSelectorMode.Drive);
            DrawSelectorButton("A", DriveSelectorMode.Auto);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawDriveButton("AWD", DriveMode.Awd);
            DrawDriveButton("RWD", DriveMode.Rwd);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label($"Mode: {(telemetry.driveMode == DriveMode.Awd ? "AWD" : "RWD")}   Selector: {telemetry.activeSelectorLabel}", labelStyle);
            GUILayout.Label($"ABS: {(controller.AbsActive ? "ACTIVE" : "On")}", labelStyle);

            ConvoyTurboController turbo = controller.ConvoyTurbo;
            if (turbo != null)
            {
                GUILayout.Label(
                    $"Shift boost: {(turbo.IsActive ? $"x{turbo.TorqueMultiplier:0.00}" : "ready")}",
                    labelStyle);
            }

            VehicleAudio vehicleAudio = controller.GetComponent<VehicleAudio>();
            if (vehicleAudio != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"Sound effects: {Mathf.RoundToInt(vehicleAudio.EffectsVolume * 100f)}%", labelStyle);
                vehicleAudio.SetEffectsVolume(GUILayout.HorizontalSlider(vehicleAudio.EffectsVolume, 0f, 1f));
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Respawn at start", buttonStyle))
            {
                controller.RespawnAtStart();
            }

            GUILayout.EndArea();
        }

        private void DrawSelectorButton(string label, DriveSelectorMode mode)
        {
            GUIStyle selectorStyle = controller.selectorMode == mode ? selectedButtonStyle : buttonStyle;
            if (GUILayout.Button(label, selectorStyle, GUILayout.Width(52f)))
            {
                controller.SetSelectorMode(mode);
            }
        }

        private void DrawDriveButton(string label, DriveMode mode)
        {
            GUIStyle driveStyle = controller.driveMode == mode ? selectedButtonStyle : buttonStyle;
            if (GUILayout.Button(label, driveStyle, GUILayout.Width(84f)))
            {
                controller.SetDriveMode(mode);
            }
        }
    }
}
