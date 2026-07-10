using UnityEngine;

namespace HeavySuvPrototype
{
    public static class HeavySuvPrototypeFactory
    {
        private const float WheelRadius = 0.34f;
        private const float WheelWidth = 0.25f;

        public static HeavySuvVehicleController CreatePrototype(bool includeCameraAndHud = true)
        {
            Physics.gravity = new Vector3(0f, -9.81f, 0f);

            CreateEnvironment();
            HeavySuvVehicleController vehicle = CreateVehicle(Vector3.zero);

            if (includeCameraAndHud)
            {
                CreateCamera(vehicle.transform);
                vehicle.gameObject.AddComponent<VehicleHud>().Bind(vehicle);
            }

            return vehicle;
        }

        public static void CreateEnvironment()
        {
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Material groundMaterial = PrototypeMaterialFactory.CreateLit(new Color(0.32f, 0.42f, 0.39f));
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Flat Ground";
            ground.transform.localScale = new Vector3(18f, 1f, 18f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            Material lineMaterial = PrototypeMaterialFactory.CreateLit(new Color(0.54f, 0.62f, 0.58f));

            for (int i = -18; i <= 18; i += 1)
            {
                CreateLine($"Grid Z {i}", new Vector3(0f, 0.012f, i * 5f), new Vector3(180f, 0.018f, 0.035f), lineMaterial);
                CreateLine($"Grid X {i}", new Vector3(i * 5f, 0.014f, 0f), new Vector3(0.035f, 0.018f, 180f), lineMaterial);
            }
        }

        private static void CreateLine(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.position = position;
            line.transform.localScale = scale;
            RemoveCollider(line);
            line.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static HeavySuvVehicleController CreateVehicle(Vector3 groundPosition)
        {
            GameObject root = new GameObject("Rally Car");
            root.transform.position = groundPosition + new Vector3(0f, 0.52f, 0f);
            root.transform.rotation = Quaternion.identity;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1550f;

            BoxCollider chassisCollider = root.AddComponent<BoxCollider>();
            chassisCollider.center = new Vector3(0f, 0.2f, 0.02f);
            chassisCollider.size = new Vector3(1.82f, 0.42f, 3.72f);

            HeavySuvVehicleController controller = root.AddComponent<HeavySuvVehicleController>();
            controller.wheels = new HeavySuvVehicleController.Wheel[4];
            controller.wheels[0] = CreateWheel(root.transform, "Front Left", true, true, -0.825f, 1.3f);
            controller.wheels[1] = CreateWheel(root.transform, "Front Right", true, false, 0.825f, 1.3f);
            controller.wheels[2] = CreateWheel(root.transform, "Rear Left", false, true, -0.825f, -1.3f);
            controller.wheels[3] = CreateWheel(root.transform, "Rear Right", false, false, 0.825f, -1.3f);

            CreateChassisVisual(root.transform);
            CreateBrakeLights(root.transform, controller);
            controller.EnsureInitialized();
            VehicleTelemetry telemetry = root.AddComponent<VehicleTelemetry>();
            telemetry.Bind(controller);
            TireMarkController tireMarks = root.AddComponent<TireMarkController>();
            tireMarks.Bind(controller);
            VehicleAudio vehicleAudio = root.AddComponent<VehicleAudio>();
            vehicleAudio.Bind(controller);
            ConvoyTurboController turbo = root.AddComponent<ConvoyTurboController>();
            turbo.SetGapState(ConvoyGapState.Invalid);
            controller.EnsureInitialized();
            return controller;
        }

        private static HeavySuvVehicleController.Wheel CreateWheel(
            Transform parent,
            string name,
            bool isFront,
            bool isLeft,
            float x,
            float z)
        {
            GameObject wheelObject = new GameObject($"{name} WheelCollider");
            wheelObject.transform.SetParent(parent, false);
            wheelObject.transform.localPosition = new Vector3(x, 0.01f, z);
            WheelCollider wheelCollider = wheelObject.AddComponent<WheelCollider>();
            wheelCollider.mass = 34f;
            wheelCollider.radius = WheelRadius;
            wheelCollider.wheelDampingRate = 0.5f;
            wheelCollider.suspensionDistance = 0.36f;
            wheelCollider.forceAppPointDistance = 0.12f;

            JointSpring spring = wheelCollider.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4500f;
            spring.targetPosition = 0.5f;
            wheelCollider.suspensionSpring = spring;

            WheelFrictionCurve forward = wheelCollider.forwardFriction;
            forward.extremumSlip = 0.32f;
            forward.extremumValue = 1.05f;
            forward.asymptoteSlip = 0.9f;
            forward.asymptoteValue = 0.62f;
            forward.stiffness = 1.05f;
            wheelCollider.forwardFriction = forward;

            WheelFrictionCurve sideways = wheelCollider.sidewaysFriction;
            sideways.extremumSlip = 0.24f;
            sideways.extremumValue = 1f;
            sideways.asymptoteSlip = 0.78f;
            sideways.asymptoteValue = 0.55f;
            sideways.stiffness = isFront ? 1.08f : 0.98f;
            wheelCollider.sidewaysFriction = sideways;

            Transform visual = CreateWheelVisual(parent, name, isLeft, x, z);
            return new HeavySuvVehicleController.Wheel
            {
                name = name,
                isFront = isFront,
                isLeft = isLeft,
                collider = wheelCollider,
                visual = visual
            };
        }

        private static Transform CreateWheelVisual(Transform parent, string name, bool isLeft, float x, float z)
        {
            GameObject pivot = new GameObject($"{name} Visual Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(x, -0.08f, z);

            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = $"{name} Tire";
            wheel.transform.SetParent(pivot.transform, false);
            wheel.transform.localPosition = Vector3.zero;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(WheelRadius * 2f, WheelWidth * 0.5f, WheelRadius * 2f);
            wheel.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.04f, 0.045f, 0.045f));
            RemoveCollider(wheel);

            Material rimMaterial = CreateMaterial(new Color(0.72f, 0.76f, 0.74f));
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = $"{name} Hub";
            hub.transform.SetParent(pivot.transform, false);
            hub.transform.localPosition = Vector3.zero;
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hub.transform.localScale = new Vector3(WheelRadius * 0.96f, (WheelWidth + 0.03f) * 0.5f, WheelRadius * 0.96f);
            hub.GetComponent<Renderer>().sharedMaterial = rimMaterial;
            RemoveCollider(hub);

            float rimSide = isLeft ? -WheelWidth * 0.54f : WheelWidth * 0.54f;
            CreateWheelSpoke(pivot.transform, $"{name} Horizontal Spoke", rimMaterial, rimSide, 0f);
            CreateWheelSpoke(pivot.transform, $"{name} Vertical Spoke", rimMaterial, rimSide, 90f);
            CreateWheelSpoke(pivot.transform, $"{name} Diagonal Spoke A", rimMaterial, rimSide, 45f);
            CreateWheelSpoke(pivot.transform, $"{name} Diagonal Spoke B", rimMaterial, rimSide, 135f);
            return pivot.transform;
        }

        private static void CreateWheelSpoke(
            Transform parent,
            string name,
            Material material,
            float sideOffset,
            float angleDegrees)
        {
            GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spoke.name = name;
            spoke.transform.SetParent(parent, false);
            spoke.transform.localPosition = new Vector3(sideOffset, 0f, 0f);
            spoke.transform.localRotation = Quaternion.Euler(angleDegrees, 0f, 0f);
            spoke.transform.localScale = new Vector3(0.055f, WheelRadius * 1.45f, WheelRadius * 0.12f);
            spoke.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(spoke);
        }

        private static void CreateChassisVisual(Transform parent)
        {
            Material bodyMaterial = CreateMaterial(new Color(0.82f, 0.16f, 0.08f));
            Material darkBodyMaterial = CreateMaterial(new Color(0.23f, 0.045f, 0.025f));
            Material glassMaterial = CreateMaterial(new Color(0.08f, 0.15f, 0.19f));

            CreateBodyPart(parent, "Rally Hatch Lower Body", bodyMaterial, new Vector3(0f, 0.18f, 0f), new Vector3(1.86f, 0.4f, 3.76f));
            CreateBodyPart(parent, "Rally Hatch Hood", bodyMaterial, new Vector3(0f, 0.44f, 1.2f), new Vector3(1.7f, 0.2f, 1.24f), -4f);
            CreateBodyPart(parent, "Rally Hatch Roof", bodyMaterial, new Vector3(0f, 0.8f, -0.22f), new Vector3(1.48f, 0.16f, 1.35f));
            CreateBodyPart(parent, "Rally Hatch Cabin", glassMaterial, new Vector3(0f, 0.62f, -0.08f), new Vector3(1.5f, 0.5f, 1.55f), 3f);
            CreateBodyPart(parent, "Rally Hatch Rear", bodyMaterial, new Vector3(0f, 0.47f, -1.43f), new Vector3(1.72f, 0.45f, 0.72f), 4f);
            CreateBodyPart(parent, "Front Bumper", darkBodyMaterial, new Vector3(0f, 0.12f, 1.91f), new Vector3(1.78f, 0.18f, 0.12f));
            CreateBodyPart(parent, "Rear Bumper", darkBodyMaterial, new Vector3(0f, 0.11f, -1.91f), new Vector3(1.78f, 0.18f, 0.12f));
            CreateBodyPart(parent, "Rear Spoiler", darkBodyMaterial, new Vector3(0f, 0.71f, -1.73f), new Vector3(1.48f, 0.08f, 0.34f), -6f);
        }

        private static void CreateBodyPart(
            Transform parent,
            string name,
            Material material,
            Vector3 position,
            Vector3 scale,
            float pitchDegrees = 0f)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(part);
        }

        private static void CreateBrakeLights(Transform parent, HeavySuvVehicleController controller)
        {
            Material brakeMaterial = CreateMaterial(new Color(0.18f, 0.01f, 0.01f));
            brakeMaterial.EnableKeyword("_EMISSION");
            Renderer[] renderers = new Renderer[2];
            Light[] lights = new Light[2];

            renderers[0] = CreateBrakeLight(parent, "Left Brake Light", -0.67f, brakeMaterial, out lights[0]);
            renderers[1] = CreateBrakeLight(parent, "Right Brake Light", 0.67f, brakeMaterial, out lights[1]);

            VehicleLights vehicleLights = parent.gameObject.AddComponent<VehicleLights>();
            vehicleLights.Bind(controller);
            vehicleLights.brakeRenderers = renderers;
            vehicleLights.brakeLights = lights;
        }

        private static Renderer CreateBrakeLight(Transform parent, string name, float x, Material material, out Light light)
        {
            GameObject brakeLight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brakeLight.name = name;
            brakeLight.transform.SetParent(parent, false);
            brakeLight.transform.localPosition = new Vector3(x, 0.44f, -1.82f);
            brakeLight.transform.localScale = new Vector3(0.34f, 0.14f, 0.05f);
            Renderer renderer = brakeLight.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            RemoveCollider(brakeLight);

            light = brakeLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.04f, 0.02f);
            light.range = 1.2f;
            light.intensity = 0f;
            light.enabled = false;
            return renderer;
        }

        public static ChaseCamera CreateCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Chase Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            cameraObject.AddComponent<AudioListener>();
            ChaseCamera chaseCamera = cameraObject.AddComponent<ChaseCamera>();
            chaseCamera.target = target;
            if (target == null)
            {
                cameraObject.transform.position = new Vector3(0f, 7.5f, -12f);
                cameraObject.transform.LookAt(new Vector3(0f, 1f, 2f), Vector3.up);
            }
            return chaseCamera;
        }

        private static Material CreateMaterial(Color color)
        {
            return PrototypeMaterialFactory.CreateLit(color);
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
