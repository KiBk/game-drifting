using HeavySuvPrototype;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeavySuvPrototype.Editor
{
    public static class PrototypeSceneBuilder
    {
        internal const string ScenePath = "Assets/Scenes/HeavySuvPrototype.unity";
        internal const string NetworkPrefabPath = "Assets/Resources/Network/NetworkRallyCar.prefab";
        internal const string MaterialFolder = "Assets/Resources/Network/Materials";

        [MenuItem("Heavy SUV/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "HeavySuvPrototype";

            EnsureVehicleLayer();
            GameObject carPrefab = BuildNetworkCarPrefab();
            HeavySuvPrototypeFactory.CreateEnvironment();
            HeavySuvPrototypeFactory.CreateCamera(null);
            CreateMultiplayerObjects(carPrefab);
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.66f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.56f, 0.68f, 0.72f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 220f;

            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"Built prototype scene at {ScenePath}");
        }

        private static GameObject BuildNetworkCarPrefab()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Network");

            HeavySuvVehicleController vehicle = HeavySuvPrototypeFactory.CreateVehicle(Vector3.zero);
            GameObject root = vehicle.gameObject;
            root.name = "Network Rally Car";
            SetLayerRecursively(root, MultiplayerCoordinator.NetworkVehicleLayer);
            AssignPersistentMaterials(root);
            root.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            MultiplayerNetworkTuning.Apply(networkTransform);
            root.AddComponent<NetworkRigidbody>();
            root.AddComponent<NetworkRallyCar>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NetworkPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateMultiplayerObjects(GameObject carPrefab)
        {
            GameObject networkManagerObject = new GameObject("Network Manager");
            NetworkManager networkManager = networkManagerObject.AddComponent<NetworkManager>();
            UnityTransport transport = networkManagerObject.AddComponent<UnityTransport>();
            MultiplayerBootstrap bootstrap = networkManagerObject.AddComponent<MultiplayerBootstrap>();
            bootstrap.carPrefab = carPrefab;
            transport.UseWebSockets = true;
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.ProtocolVersion = MultiplayerBootstrap.NetworkProtocolVersion;
            networkManager.NetworkConfig.TickRate = MultiplayerNetworkTuning.TickRate;

            GameObject coordinatorObject = new GameObject("Multiplayer Coordinator");
            MultiplayerCoordinator coordinator = coordinatorObject.AddComponent<MultiplayerCoordinator>();
            coordinator.carPrefab = carPrefab;

            GameObject hudObject = new GameObject("Multiplayer HUD");
            MultiplayerHud hud = hudObject.AddComponent<MultiplayerHud>();
            hud.bootstrap = bootstrap;
            hud.coordinator = coordinator;
        }

        private static void EnsureVehicleLayer()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(MultiplayerCoordinator.NetworkVehicleLayer).stringValue = "NetworkVehicle";
            tagManager.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }

        private static void AssignPersistentMaterials(GameObject root)
        {
            EnsureFolder(MaterialFolder);
            Material body = GetOrCreateMaterial("RallyBody", new Color(0.82f, 0.16f, 0.08f));
            Material dark = GetOrCreateMaterial("RallyDark", new Color(0.23f, 0.045f, 0.025f));
            Material glass = GetOrCreateMaterial("RallyGlass", new Color(0.08f, 0.15f, 0.19f));
            Material tire = GetOrCreateMaterial("RallyTire", new Color(0.035f, 0.038f, 0.042f));
            Material rim = GetOrCreateMaterial("RallyRim", new Color(0.62f, 0.65f, 0.68f));
            Material brake = GetOrCreateMaterial("RallyBrake", new Color(0.18f, 0.01f, 0.01f));

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.gameObject.name;
                if (name.Contains("Tire"))
                {
                    renderer.sharedMaterial = tire;
                }
                else if (name.Contains("Hub") || name.Contains("Spoke"))
                {
                    renderer.sharedMaterial = rim;
                }
                else if (name.Contains("Cabin"))
                {
                    renderer.sharedMaterial = glass;
                }
                else if (name.Contains("Bumper") || name.Contains("Spoiler"))
                {
                    renderer.sharedMaterial = dark;
                }
                else if (name.Contains("Brake Light"))
                {
                    renderer.sharedMaterial = brake;
                }
                else
                {
                    renderer.sharedMaterial = body;
                }
            }
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
