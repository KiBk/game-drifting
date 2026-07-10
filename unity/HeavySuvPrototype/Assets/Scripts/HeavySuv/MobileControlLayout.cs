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
            input.turbo |= controls.Boost.Contains(guiPosition);
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
