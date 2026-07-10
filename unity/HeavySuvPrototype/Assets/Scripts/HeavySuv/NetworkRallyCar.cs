using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace HeavySuvPrototype
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRallyCar : NetworkBehaviour
    {
        private static readonly string[] PaintedPartNames =
        {
            "Rally Hatch Lower Body",
            "Rally Hatch Hood",
            "Rally Hatch Roof",
            "Rally Hatch Rear"
        };

        private readonly NetworkVariable<uint> colorRgba = new NetworkVariable<uint>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<byte> bodyStyleValue = new NetworkVariable<byte>(
            (byte)VehicleBodyStyle.RallyHatch,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        private HeavySuvVehicleController controller;
        private VehicleAppearanceController appearance;

        public Color32 VehicleColor => UnpackColor(colorRgba.Value);
        public VehicleBodyStyle BodyStyle => DecodeBodyStyle(bodyStyleValue.Value);
        public bool LocalSimulationEnabled => controller != null && controller.enabled;
        public NetworkVariableWritePermission ColorWritePermission => colorRgba.WritePerm;
        public NetworkVariableWritePermission BodyStyleWritePermission => bodyStyleValue.WritePerm;

        private void Awake()
        {
            controller = GetComponent<HeavySuvVehicleController>();
            appearance = GetComponent<VehicleAppearanceController>();
            MultiplayerNetworkTuning.Apply(GetComponent<NetworkTransform>());
        }

        public override void OnNetworkSpawn()
        {
            colorRgba.OnValueChanged += OnColorChanged;
            bodyStyleValue.OnValueChanged += OnBodyStyleChanged;
            ApplyColor(UnpackColor(colorRgba.Value));
            ApplyBodyStyle(DecodeBodyStyle(bodyStyleValue.Value));
            ConfigureAuthority(IsOwner);
        }

        public override void OnNetworkDespawn()
        {
            colorRgba.OnValueChanged -= OnColorChanged;
            bodyStyleValue.OnValueChanged -= OnBodyStyleChanged;
        }

        public void SetColor(Color32 color)
        {
            uint packedColor = PackColor(color);
            if (IsOwner)
            {
                colorRgba.Value = packedColor;
            }
            else if (IsServer)
            {
                SetColorForOwnerRpc(packedColor);
            }
        }

        public void SetBodyStyle(VehicleBodyStyle style)
        {
            byte encodedStyle = (byte)style;
            if (IsOwner)
            {
                bodyStyleValue.Value = encodedStyle;
            }
            else if (IsServer)
            {
                SetBodyStyleForOwnerRpc(encodedStyle);
            }
        }

        [Rpc(SendTo.Owner)]
        private void SetColorForOwnerRpc(uint packedColor)
        {
            colorRgba.Value = packedColor;
        }

        [Rpc(SendTo.Owner)]
        private void SetBodyStyleForOwnerRpc(byte encodedStyle)
        {
            bodyStyleValue.Value = encodedStyle;
        }

        public static uint PackColor(Color32 color)
        {
            return (uint)(color.r | color.g << 8 | color.b << 16 | color.a << 24);
        }

        public static Color32 UnpackColor(uint rgba)
        {
            return new Color32(
                (byte)(rgba & 0xff),
                (byte)((rgba >> 8) & 0xff),
                (byte)((rgba >> 16) & 0xff),
                (byte)((rgba >> 24) & 0xff));
        }

        private static VehicleBodyStyle DecodeBodyStyle(byte encodedStyle)
        {
            return encodedStyle == (byte)VehicleBodyStyle.KenneySport
                ? VehicleBodyStyle.KenneySport
                : VehicleBodyStyle.RallyHatch;
        }

        private void ConfigureAuthority(bool isOwner)
        {
            if (controller != null)
            {
                controller.useKeyboardInput = isOwner;
                controller.enabled = isOwner;
            }

            SetComponentEnabled<VehicleAudio>(isOwner);
            SetComponentEnabled<TireMarkController>(isOwner);
            foreach (AudioSource source in GetComponents<AudioSource>())
            {
                source.enabled = isOwner;
            }

            if (!isOwner)
            {
                return;
            }

            FindAnyObjectByType<MultiplayerBootstrap>()?.NotifyLocalCarReady();

            ChaseCamera camera = FindAnyObjectByType<ChaseCamera>();
            if (camera != null)
            {
                camera.target = transform;
            }

            VehicleHud hud = GetComponent<VehicleHud>();
            if (hud == null)
            {
                hud = gameObject.AddComponent<VehicleHud>();
            }

            hud.Bind(controller);
        }

        private void SetComponentEnabled<T>(bool enabled) where T : Behaviour
        {
            T component = GetComponent<T>();
            if (component != null)
            {
                component.enabled = enabled;
            }
        }

        private void OnColorChanged(uint previous, uint current)
        {
            ApplyColor(UnpackColor(current));
        }

        private void OnBodyStyleChanged(byte previous, byte current)
        {
            ApplyBodyStyle(DecodeBodyStyle(current));
        }

        private void ApplyBodyStyle(VehicleBodyStyle style)
        {
            appearance ??= GetComponent<VehicleAppearanceController>();
            appearance?.SetBodyStyle(style);
        }

        private void ApplyColor(Color32 color)
        {
            foreach (string partName in PaintedPartNames)
            {
                Transform part = transform.Find(partName);
                Renderer renderer = part == null ? null : part.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = color;
                }
            }
        }
    }
}
