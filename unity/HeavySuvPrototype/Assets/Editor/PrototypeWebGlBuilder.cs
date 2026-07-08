using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HeavySuvPrototype.Editor
{
    public static class PrototypeWebGlBuilder
    {
        private const string BuildPath = "Builds/WebGL";

        [MenuItem("Heavy SUV/Build WebGL Prototype")]
        public static void BuildWebGl()
        {
            PrototypeSceneBuilder.BuildPrototypeScene();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { PrototypeSceneBuilder.ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"WebGL build failed with result {summary.result} and {summary.totalErrors} errors.");
            }

            Debug.Log($"Built WebGL prototype at {BuildPath} ({summary.totalSize} bytes)");
        }
    }
}
