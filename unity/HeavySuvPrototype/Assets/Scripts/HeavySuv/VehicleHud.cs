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
            builder.Clear();
            builder.AppendLine("Unity WheelCollider SUV");
            builder.Append("Speed: ").Append(Mathf.RoundToInt(telemetry.speedKmh)).AppendLine(" km/h");
            builder.Append("Signed: ").Append(telemetry.signedSpeedMetersPerSecond.ToString("0.0")).AppendLine(" m/s");
            builder.Append("Heading: ").Append(telemetry.headingDegrees.ToString("0.0")).AppendLine(" deg");
            builder.Append("Position: ").Append(telemetry.position.ToString("F2")).AppendLine();
            builder.Append("Drive: ").AppendLine(telemetry.driveMode == DriveMode.Awd ? "AWD" : "RWD");
            builder.Append("Gear: ").AppendLine(telemetry.activeGearLabel);
            builder.AppendLine("Keys: arrows drive, Space handbrake, D toggles AWD/RWD");
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

            GUI.Box(new Rect(16f, 16f, 360f, 272f), builder.ToString(), style);
            DrawControlPanel(telemetry);
        }

        private void DrawControlPanel(VehicleTelemetrySample telemetry)
        {
            const float panelWidth = 292f;
            Rect panel = new Rect(Screen.width - panelWidth - 16f, 16f, panelWidth, 184f);
            GUI.Box(panel, string.Empty, controlStyle);

            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 12f, panel.width - 28f, panel.height - 24f));
            GUILayout.Label("Gearbox", labelStyle);
            GUILayout.BeginHorizontal();
            DrawGearButton("R", GearboxMode.Reverse);
            DrawGearButton("N", GearboxMode.Neutral);
            DrawGearButton("1", GearboxMode.First);
            DrawGearButton("2", GearboxMode.Second);
            DrawGearButton("A", GearboxMode.Auto);
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label($"Power: {Mathf.RoundToInt(telemetry.engineTorque)}", labelStyle);

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawDriveButton("AWD", DriveMode.Awd);
            DrawDriveButton("RWD", DriveMode.Rwd);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label($"Mode: {(telemetry.driveMode == DriveMode.Awd ? "AWD" : "RWD")}   Active: {telemetry.activeGearLabel}", labelStyle);
            GUILayout.EndArea();
        }

        private void DrawGearButton(string label, GearboxMode mode)
        {
            GUIStyle gearStyle = controller.gearboxMode == mode ? selectedButtonStyle : buttonStyle;
            if (GUILayout.Button(label, gearStyle, GUILayout.Width(42f)))
            {
                controller.SetGearboxMode(mode);
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
