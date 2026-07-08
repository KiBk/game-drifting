using HeavySuvPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeavySuvPrototype.Editor
{
    public static class PrototypeSceneBuilder
    {
        internal const string ScenePath = "Assets/Scenes/HeavySuvPrototype.unity";

        [MenuItem("Heavy SUV/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "HeavySuvPrototype";

            HeavySuvPrototypeFactory.CreatePrototype(includeCameraAndHud: true);
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
    }
}
