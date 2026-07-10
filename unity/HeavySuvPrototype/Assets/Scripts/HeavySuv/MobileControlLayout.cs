using System;
using UnityEngine;

namespace HeavySuvPrototype
{
    public struct MobileControlRects
    {
        public Rect SteerLeft;
        public Rect SteerRight;
        public Rect Brake;
        public Rect Throttle;
        public Rect Boost;
    }

    public static class MobileControlLayout
    {
        public const string ForceQuery = "touchControls=1";
        public const float AutomaticPhoneShortSide = 540f;

        public static bool ShouldEnable(bool touchSupported, bool mobilePlatform, string absoluteUrl)
        {
            return touchSupported || mobilePlatform || IsForced(absoluteUrl);
        }

        public static bool IsForced(string absoluteUrl)
        {
            return !string.IsNullOrWhiteSpace(absoluteUrl) &&
                   absoluteUrl.IndexOf(ForceQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsLandscape(int width, int height)
        {
            return width >= height;
        }

        public static bool ShouldShowControlsByDefault(
            bool mobilePlatform,
            float width,
            float height,
            string absoluteUrl)
        {
            return mobilePlatform ||
                   IsForced(absoluteUrl) ||
                   Mathf.Min(width, height) <= AutomaticPhoneShortSide;
        }

        public static float GetUiScale(float height)
        {
            return Mathf.Clamp(height / 520f, 0.82f, 1.35f);
        }

        public static Rect GetToolbarButtonRect(
            float width,
            float height,
            float scale,
            int indexFromRight)
        {
            float margin = GetToolbarMargin(height);
            float buttonWidth = GetToolbarButtonWidth(scale);
            float buttonHeight = Mathf.Clamp(52f * scale, 44f, 64f);
            float gap = GetToolbarGap(scale);
            float x = width - margin - buttonWidth -
                      Mathf.Max(0, indexFromRight) * (buttonWidth + gap);
            return new Rect(x, margin, buttonWidth, buttonHeight);
        }

        public static float GetToolbarReservedWidth(float height, float scale, int buttonCount)
        {
            if (buttonCount <= 0)
            {
                return 0f;
            }

            return GetToolbarMargin(height) +
                   GetToolbarButtonWidth(scale) * buttonCount +
                   GetToolbarGap(scale) * (buttonCount - 1);
        }

        public static MobileControlRects Calculate(float width, float height)
        {
            float controlSize = Mathf.Clamp(height * 0.24f, 68f, 132f);
            float gap = controlSize * 0.12f;
            float margin = Mathf.Clamp(height * 0.035f, 12f, 28f);
            float bottom = height - controlSize - margin;
            float right = width - margin - controlSize;

            return new MobileControlRects
            {
                SteerLeft = new Rect(margin, bottom, controlSize, controlSize),
                SteerRight = new Rect(margin + controlSize + gap, bottom, controlSize, controlSize),
                Brake = new Rect(right - controlSize - gap, bottom, controlSize, controlSize),
                Throttle = new Rect(right, bottom, controlSize, controlSize),
                Boost = new Rect(
                    right,
                    bottom - controlSize * 0.62f - gap,
                    controlSize,
                    controlSize * 0.56f)
            };
        }

        public static void ApplyPointer(
            ref VehicleInputState input,
            MobileControlRects controls,
            Vector2 guiPosition)
        {
            input.steerLeft |= controls.SteerLeft.Contains(guiPosition);
            input.steerRight |= controls.SteerRight.Contains(guiPosition);
            input.brake |= controls.Brake.Contains(guiPosition);
            input.throttle |= controls.Throttle.Contains(guiPosition);
            bool boostPressed = controls.Boost.Contains(guiPosition);
            input.throttle |= boostPressed;
            input.turbo |= boostPressed;
        }

        private static float GetToolbarMargin(float height)
        {
            return Mathf.Clamp(height * 0.025f, 8f, 18f);
        }

        private static float GetToolbarButtonWidth(float scale)
        {
            return Mathf.Clamp(104f * scale, 84f, 132f);
        }

        private static float GetToolbarGap(float scale)
        {
            return Mathf.Clamp(7f * scale, 5f, 10f);
        }

        public static void RequestLandscapeOrientation()
        {
#if !UNITY_WEBGL
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
        }
    }
}
