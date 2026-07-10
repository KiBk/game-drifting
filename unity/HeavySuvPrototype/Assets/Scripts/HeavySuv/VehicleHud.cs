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
        private GUIStyle touchControlStyle;
        private MultiplayerBootstrap bootstrap;
        private VehicleInputState mobileInput;
        private bool mobileControlsEnabled;
        private bool mobileStateInitialized;
        private bool mobilePanelOpen;
        private float mobilePanelVisibility;

        public VehicleInputState MobileInput => mobileInput;
        public bool MobileControlsEnabled => mobileControlsEnabled;

        public void Bind(HeavySuvVehicleController vehicleController)
        {
            controller = vehicleController;
            bootstrap ??= FindAnyObjectByType<MultiplayerBootstrap>();
            RefreshMobileMode();
        }

        private void OnEnable()
        {
            RefreshMobileMode();
        }

        private void Update()
        {
            RefreshMobileMode();
            mobilePanelVisibility = Mathf.MoveTowards(
                mobilePanelVisibility,
                mobilePanelOpen ? 1f : 0f,
                Time.unscaledDeltaTime * 5f);
            UpdateMobileInput();
        }

        private void OnGUI()
        {
            if (controller == null)
            {
                return;
            }

            GUI.depth = -10;
            float scale = mobileControlsEnabled ? GetMobileScale() : 1f;
            EnsureStyles(mobileControlsEnabled, scale);
            if (mobileControlsEnabled && !MobileControlLayout.IsLandscape(Screen.width, Screen.height))
            {
                return;
            }

            VehicleTelemetrySample telemetry = controller.CaptureTelemetry();
            if (!mobileControlsEnabled)
            {
                DrawControlPanel(
                    new Rect(Screen.width - 308f, 16f, 292f, 318f),
                    telemetry,
                    scale);
                return;
            }

            if (CanDrive())
            {
                DrawTouchControls(MobileControlLayout.Calculate(Screen.width, Screen.height));
            }

            if (mobilePanelVisibility > 0.01f)
            {
                DrawControlPanel(GetMobilePanelRect(scale), telemetry, scale);
            }

            DrawMobilePanelToggle(scale);
        }

        private void RefreshMobileMode()
        {
            bool enabled = MobileControlLayout.ShouldEnable(
                Input.touchSupported,
                Application.isMobilePlatform,
                Application.absoluteURL);
            if (mobileStateInitialized && enabled == mobileControlsEnabled)
            {
                return;
            }

            mobileStateInitialized = true;
            mobileControlsEnabled = enabled;
            mobilePanelOpen = !enabled;
            mobilePanelVisibility = enabled ? 0f : 1f;
            mobileInput = VehicleInputState.None;
            if (enabled)
            {
                MobileControlLayout.RequestLandscapeOrientation();
            }
        }

        private void UpdateMobileInput()
        {
            mobileInput = VehicleInputState.None;
            if (!mobileControlsEnabled ||
                !MobileControlLayout.IsLandscape(Screen.width, Screen.height) ||
                !CanDrive())
            {
                return;
            }

            MobileControlRects controls = MobileControlLayout.Calculate(Screen.width, Screen.height);
            float scale = GetMobileScale();
            for (int touchIndex = 0; touchIndex < Input.touchCount; touchIndex += 1)
            {
                Touch touch = Input.GetTouch(touchIndex);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    continue;
                }

                Vector2 guiPosition = new Vector2(touch.position.x, Screen.height - touch.position.y);
                ApplyDrivingPointer(controls, guiPosition, scale);
            }

            if (Input.touchCount == 0 && Input.GetMouseButton(0))
            {
                Vector2 mousePosition = Input.mousePosition;
                Vector2 guiPosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
                ApplyDrivingPointer(controls, guiPosition, scale);
            }
        }

        private void ApplyDrivingPointer(MobileControlRects controls, Vector2 guiPosition, float scale)
        {
            if (GetMobilePanelToggleRect(scale).Contains(guiPosition) ||
                (mobilePanelVisibility > 0.01f && GetMobilePanelRect(scale).Contains(guiPosition)))
            {
                return;
            }

            MobileControlLayout.ApplyPointer(ref mobileInput, controls, guiPosition);
        }

        private bool CanDrive()
        {
            bootstrap ??= FindAnyObjectByType<MultiplayerBootstrap>();
            return bootstrap == null || bootstrap.IsGameplayReady;
        }

        private static float GetMobileScale()
        {
            return Mathf.Clamp(Screen.height / 520f, 0.82f, 1.35f);
        }

        private Rect GetMobilePanelRect(float scale)
        {
            float margin = Mathf.Clamp(Screen.height * 0.025f, 8f, 18f);
            float toggleHeight = Mathf.Clamp(52f * scale, 44f, 64f);
            float panelY = margin + toggleHeight + 6f;
            float panelWidth = Mathf.Min(Screen.width * 0.44f, 340f * scale);
            float openX = Screen.width - panelWidth - margin;
            float easedVisibility = mobilePanelVisibility * mobilePanelVisibility *
                                    (3f - 2f * mobilePanelVisibility);
            float panelX = Mathf.Lerp(Screen.width + margin, openX, easedVisibility);
            return new Rect(
                panelX,
                panelY,
                panelWidth,
                Mathf.Max(260f, Screen.height - panelY - margin));
        }

        private void DrawMobilePanelToggle(float scale)
        {
            Rect button = GetMobilePanelToggleRect(scale);
            if (GUI.Button(button, mobilePanelOpen ? "HIDE" : "CAR", buttonStyle))
            {
                mobilePanelOpen = !mobilePanelOpen;
            }
        }

        private static Rect GetMobilePanelToggleRect(float scale)
        {
            float margin = Mathf.Clamp(Screen.height * 0.025f, 8f, 18f);
            float width = Mathf.Clamp(88f * scale, 72f, 118f);
            float height = Mathf.Clamp(52f * scale, 44f, 64f);
            return new Rect(Screen.width - width - margin, margin, width, height);
        }

        private void DrawTouchControls(MobileControlRects controls)
        {
            DrawTouchControl(controls.SteerLeft, "LEFT", mobileInput.steerLeft, new Color(0.35f, 0.68f, 1f, 0.95f));
            DrawTouchControl(controls.SteerRight, "RIGHT", mobileInput.steerRight, new Color(0.35f, 0.68f, 1f, 0.95f));
            DrawTouchControl(controls.Brake, "BRAKE", mobileInput.brake, new Color(1f, 0.38f, 0.3f, 0.95f));
            DrawTouchControl(controls.Throttle, "GAS", mobileInput.throttle, new Color(0.32f, 0.9f, 0.42f, 0.95f));
            DrawTouchControl(controls.Boost, "BOOST", mobileInput.turbo, new Color(1f, 0.72f, 0.22f, 0.95f));
        }

        private void DrawTouchControl(Rect rect, string label, bool active, Color activeColor)
        {
            Color previousColor = GUI.color;
            GUI.color = active ? activeColor : new Color(1f, 1f, 1f, 0.7f);
            GUI.Box(rect, label, touchControlStyle);
            GUI.color = previousColor;
        }

        private void DrawControlPanel(Rect panel, VehicleTelemetrySample telemetry, float scale)
        {
            GUI.Box(panel, string.Empty, controlStyle);

            float padding = 14f * scale;
            GUILayout.BeginArea(new Rect(
                panel.x + padding,
                panel.y + 12f * scale,
                panel.width - padding * 2f,
                panel.height - 24f * scale));
            GUILayout.Label("Drive selector", labelStyle);
            GUILayout.BeginHorizontal();
            DrawSelectorButton("R", DriveSelectorMode.Reverse, scale);
            DrawSelectorButton("N", DriveSelectorMode.Neutral, scale);
            DrawSelectorButton("D", DriveSelectorMode.Drive, scale);
            DrawSelectorButton("A", DriveSelectorMode.Auto, scale);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f * scale);
            GUILayout.BeginHorizontal();
            DrawDriveButton("AWD", DriveMode.Awd, scale);
            DrawDriveButton("RWD", DriveMode.Rwd, scale);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f * scale);
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
                GUILayout.Space(6f * scale);
                GUILayout.Label($"Sound effects: {Mathf.RoundToInt(vehicleAudio.EffectsVolume * 100f)}%", labelStyle);
                vehicleAudio.SetEffectsVolume(GUILayout.HorizontalSlider(
                    vehicleAudio.EffectsVolume,
                    0f,
                    1f,
                    GUILayout.Height(24f * scale)));
            }

            GUILayout.Space(8f * scale);
            if (GUILayout.Button("Respawn at start", buttonStyle))
            {
                controller.RespawnAtStart();
            }

            GUILayout.EndArea();
        }

        private void DrawSelectorButton(string label, DriveSelectorMode mode, float scale)
        {
            GUIStyle selectorStyle = controller.selectorMode == mode ? selectedButtonStyle : buttonStyle;
            if (GUILayout.Button(label, selectorStyle, GUILayout.Width(52f * scale)))
            {
                controller.SetSelectorMode(mode);
            }
        }

        private void DrawDriveButton(string label, DriveMode mode, float scale)
        {
            GUIStyle driveStyle = controller.driveMode == mode ? selectedButtonStyle : buttonStyle;
            if (GUILayout.Button(label, driveStyle, GUILayout.Width(84f * scale)))
            {
                controller.SetDriveMode(mode);
            }
        }

        private void EnsureStyles(bool mobile, float scale)
        {
            controlStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
                padding = new RectOffset(14, 14, 12, 12)
            };
            buttonStyle ??= new GUIStyle(GUI.skin.button);
            selectedButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                normal = { textColor = Color.yellow },
                fontStyle = FontStyle.Bold
            };
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white }
            };
            touchControlStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            int panelFontSize = mobile
                ? Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.038f), 14, 22)
                : 15;
            int buttonFontSize = mobile
                ? Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.042f), 15, 24)
                : 15;
            controlStyle.fontSize = panelFontSize;
            labelStyle.fontSize = panelFontSize;
            buttonStyle.fontSize = buttonFontSize;
            selectedButtonStyle.fontSize = buttonFontSize;
            buttonStyle.fixedHeight = mobile ? Mathf.Clamp(38f * scale, 32f, 48f) : 30f;
            selectedButtonStyle.fixedHeight = buttonStyle.fixedHeight;
            touchControlStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.04f), 16, 28);
        }
    }
}
