using System.Text;
using UnityEngine;

namespace HeavySuvPrototype
{
    public sealed class VehicleHud : MonoBehaviour
    {
        private readonly StringBuilder builder = new StringBuilder(512);
        [SerializeField] private HeavySuvVehicleController controller;
        private GUIStyle style;
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

            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                normal = { textColor = Color.white },
                padding = new RectOffset(14, 14, 12, 12)
            };
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
            ConvoyTurboController turbo = controller.ConvoyTurbo;
            builder.Clear();
            builder.AppendLine("Rally Car - Convoy Prototype");
            builder.Append("Speed: ").Append(Mathf.RoundToInt(telemetry.speedKmh)).AppendLine(" km/h");
            builder.Append("Signed: ").Append(telemetry.signedSpeedMetersPerSecond.ToString("0.0")).AppendLine(" m/s");
            builder.Append("Heading: ").Append(telemetry.headingDegrees.ToString("0.0")).AppendLine(" deg");
            builder.Append("Slip angle: ").Append(telemetry.slipAngleDegrees.ToString("0.0")).AppendLine(" deg");
            builder.Append("Countersteer: ").Append((telemetry.countersteerAssistInput * 100f).ToString("0")).AppendLine("%");
            builder.Append("Position: ").Append(telemetry.position.ToString("F2")).AppendLine();
            builder.Append("Drive: ").AppendLine(telemetry.driveMode == DriveMode.Awd ? "AWD" : "RWD");
            builder.Append("Selector: ").AppendLine(telemetry.activeSelectorLabel);
            builder.Append("Traction delivery: ").Append((controller.TractionDelivery * 100f).ToString("0")).AppendLine("%");
            if (turbo != null)
            {
                builder.Append("Boost: x")
                    .Append(turbo.TorqueMultiplier.ToString("0.00"))
                    .Append(turbo.IsActive ? " ACTIVE" : " ready")
                    .AppendLine();
            }

            builder.AppendLine("Keys: arrows drive, Space handbrake, Shift boost, D AWD/RWD");
            for (int i = 0; i < telemetry.wheels.Length; i += 1)
            {
                WheelTelemetry wheel = telemetry.wheels[i];
                builder.Append(wheel.name)
                    .Append(": ")
                    .Append(wheel.grounded ? "ground " : "air ")
                    .Append("rpm ")
                    .Append(wheel.rpm.ToString("0"))
                    .Append(" slip ")
                    .Append(wheel.forwardSlip.ToString("0.00"))
                    .Append("/")
                    .Append(wheel.sidewaysSlip.ToString("0.00"))
                    .AppendLine();
            }

            GUI.Box(new Rect(16f, 16f, 390f, 346f), builder.ToString(), style);
            DrawControlPanel(telemetry);
        }

        private void DrawControlPanel(VehicleTelemetrySample telemetry)
        {
            const float panelWidth = 292f;
            Rect panel = new Rect(Screen.width - panelWidth - 16f, 16f, panelWidth, 292f);
            GUI.Box(panel, string.Empty, controlStyle);

            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 12f, panel.width - 28f, panel.height - 24f));
            GUILayout.Label("Drive selector", labelStyle);
            GUILayout.BeginHorizontal();
            DrawSelectorButton("R", DriveSelectorMode.Reverse);
            DrawSelectorButton("N", DriveSelectorMode.Neutral);
            DrawSelectorButton("D", DriveSelectorMode.Drive);
            DrawSelectorButton("A", DriveSelectorMode.Auto);
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label($"Motor torque: {Mathf.RoundToInt(telemetry.motorTorque)}", labelStyle);

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawDriveButton("AWD", DriveMode.Awd);
            DrawDriveButton("RWD", DriveMode.Rwd);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label($"Mode: {(telemetry.driveMode == DriveMode.Awd ? "AWD" : "RWD")}   Selector: {telemetry.activeSelectorLabel}", labelStyle);
            controller.countersteerAssistEnabled = GUILayout.Toggle(
                controller.countersteerAssistEnabled,
                "Keyboard countersteer assist");

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
