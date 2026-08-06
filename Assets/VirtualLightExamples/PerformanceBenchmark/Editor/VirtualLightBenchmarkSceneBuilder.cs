using System;
using System.IO;
using System.Linq;
using MizoTake.VirtualLight;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MizoTake.VirtualLight.PerformanceBenchmark.Editor
{
    public static class VirtualLightBenchmarkSceneBuilder
    {
        public const string RootPath = "Assets/VirtualLightExamples/PerformanceBenchmark";
        public const string ScenePath = RootPath + "/Scenes/VirtualLightPerformanceBenchmark.unity";
        public const string StandardMaterialPath = RootPath + "/Materials/Benchmark Standard.mat";
        public const string VirtualMaterialPath = RootPath + "/Materials/Benchmark Virtual.mat";
        public const string CasterMaterialPath = RootPath + "/Materials/Benchmark Caster.mat";
        public const string VirtualCasterMaterialPath = RootPath + "/Materials/Benchmark Virtual Caster.mat";
        public const string PlayerPath = "Builds/VirtualLightPerformanceBenchmark/VirtualLightPerformanceBenchmark.exe";
        private const string PcPipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";
        private const string GlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings 1.asset";
        private const string ResourcesPath = "Assets/Resources";
        private const int LightCapacity = 128;
        private const float LightRange = 14f;
        private const float LightIntensity = 8f;
        private const float InnerSpotAngle = 50f;
        private const float OuterSpotAngle = 70f;

        [MenuItem("Tools/Virtual Light/Rebuild Performance Benchmark Scene")]
        public static void RebuildScene()
        {
            EnsureFolder(RootPath + "/Scenes");
            EnsureFolder(RootPath + "/Materials");
            var standardMaterial = CreateOrUpdateMaterial(StandardMaterialPath, "Universal Render Pipeline/Lit", false, new Color(0.72f, 0.76f, 0.82f));
            var virtualMaterial = CreateOrUpdateMaterial(VirtualMaterialPath, "MizoTake/Virtual Light/Benchmark Receiver", false, new Color(0.72f, 0.76f, 0.82f));
            var casterMaterial = CreateOrUpdateMaterial(CasterMaterialPath, "Universal Render Pipeline/Lit", false, new Color(0.16f, 0.19f, 0.24f));
            var virtualCasterMaterial = CreateOrUpdateMaterial(VirtualCasterMaterialPath, "MizoTake/Virtual Light/Benchmark Receiver", false, new Color(0.16f, 0.19f, 0.24f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "VirtualLightPerformanceBenchmark";
            ConfigureRenderSettings();
            var root = new GameObject("Virtual Light Performance Benchmark");
            var presentation = CreateChild(root.transform, "Presentation");
            var environment = CreateChild(root.transform, "Environment");
            var lighting = CreateChild(root.transform, "Lighting");
            var runtime = CreateChild(root.transform, "Runtime");
            var standardReceivers = CreateReceiverSet(presentation.transform, "Standard Receivers", standardMaterial);
            var virtualReceivers = CreateReceiverSet(presentation.transform, "Virtual Receivers", virtualMaterial);
            virtualReceivers.SetActive(false);
            CreateCasterSets(environment.transform, casterMaterial, virtualCasterMaterial, out var standardCasters, out var virtualCasters);
            var standardLights = CreateStandardLights(lighting.transform);
            var virtualLights = CreateVirtualLights(lighting.transform);
            var camera = CreateCamera(root.transform);
            var controller = runtime.AddComponent<VirtualLightBenchmarkController>();
            AssignControllerReferences(controller, standardLights, virtualLights, standardReceivers, virtualReceivers, standardCasters, virtualCasters, camera);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddBenchmarkSceneFirst();
            PlayerSettings.enableFrameTimingStats = true;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"Performance benchmark scene rebuilt: {ScenePath}");
        }

        [MenuItem("Tools/Virtual Light/Build Performance Benchmark Windows Player")]
        public static void BuildWindowsPlayer()
        {
            if (!File.Exists(ScenePath)) RebuildScene();
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath) ?? "Builds");
            PlayerSettings.enableFrameTimingStats = true;
            var pipelineAssetSnapshot = File.ReadAllBytes(PcPipelineAssetPath);
            var globalSettingsSnapshot = File.ReadAllBytes(GlobalSettingsPath);
            var resourcesDirectoryExisted = Directory.Exists(ResourcesPath);
            var resourcesMetaExisted = File.Exists(ResourcesPath + ".meta");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = PlayerPath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            }
            finally
            {
                RestoreFile(PcPipelineAssetPath, pipelineAssetSnapshot);
                RestoreFile(GlobalSettingsPath, globalSettingsSnapshot);
                CleanupGeneratedResourcesFolder(resourcesDirectoryExisted, resourcesMetaExisted);
            }
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException($"Performance benchmark Player build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            Debug.Log($"Performance benchmark Player built: {Path.GetFullPath(PlayerPath)} ({report.summary.totalSize} bytes)");
        }

        private static void RestoreFile(string path, byte[] contents)
        {
            if (File.ReadAllBytes(path).SequenceEqual(contents)) return;
            File.WriteAllBytes(path, contents);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void CleanupGeneratedResourcesFolder(bool directoryExisted, bool metaExisted)
        {
            if (!directoryExisted && Directory.Exists(ResourcesPath) && !Directory.EnumerateFileSystemEntries(ResourcesPath).Any()) Directory.Delete(ResourcesPath);
            if (!metaExisted && File.Exists(ResourcesPath + ".meta")) File.Delete(ResourcesPath + ".meta");
        }

        private static Material CreateOrUpdateMaterial(string path, string shaderName, bool disableStandardLighting, Color color)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader not found: {shaderName}");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (disableStandardLighting)
            {
                material.SetFloat("_ReceiveStandardLighting", 0f);
                material.EnableKeyword("_RECEIVE_STANDARD_LIGHTING_OFF");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
        }

        private static GameObject CreateReceiverSet(Transform parent, string name, Material material)
        {
            var root = CreateChild(parent, name);
            CreatePrimitive(root.transform, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.1f, 3f), new Vector3(12f, 0.2f, 12f), material, ShadowCastingMode.Off, true);
            CreatePrimitive(root.transform, "Back Wall", PrimitiveType.Cube, new Vector3(0f, 2.5f, 8.9f), new Vector3(12f, 5f, 0.2f), material, ShadowCastingMode.Off, true);
            return root;
        }

        private static void CreateCasterSets(Transform parent, Material standardMaterial, Material virtualMaterial, out GameObject standardCasters, out GameObject virtualCasters)
        {
            standardCasters = CreateChild(parent, "Standard Shadow Casters");
            CreateCasterGeometry(standardCasters.transform, standardMaterial);
            virtualCasters = CreateChild(parent, "Virtual Shadow Casters");
            virtualCasters.AddComponent<VirtualLightOccluder>();
            CreateCasterGeometry(virtualCasters.transform, virtualMaterial);
            virtualCasters.SetActive(false);
        }

        private static void CreateCasterGeometry(Transform root, Material material)
        {
            CreatePrimitive(root, "Center Caster", PrimitiveType.Cube, new Vector3(0f, 1f, 3f), new Vector3(1.6f, 2f, 1.6f), material, ShadowCastingMode.On, true);
            CreatePrimitive(root, "Left Caster", PrimitiveType.Sphere, new Vector3(-2.2f, 0.75f, 4.2f), new Vector3(1.5f, 1.5f, 1.5f), material, ShadowCastingMode.On, true);
            CreatePrimitive(root, "Right Caster", PrimitiveType.Capsule, new Vector3(2.2f, 1f, 4.2f), new Vector3(1.3f, 1.3f, 1.3f), material, ShadowCastingMode.On, true);
        }

        private static Light[] CreateStandardLights(Transform parent)
        {
            var root = CreateChild(parent, "Standard Rig");
            var lights = new Light[LightCapacity];
            for (var index = 0; index < lights.Length; index++)
            {
                var lightObject = CreateChild(root.transform, $"Standard Light {index + 1:D3}");
                SetLightTransform(lightObject.transform, index);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.92f, 0.82f);
                light.intensity = LightIntensity;
                light.range = LightRange;
                light.spotAngle = OuterSpotAngle;
                light.innerSpotAngle = InnerSpotAngle;
                light.shadows = LightShadows.None;
                light.shadowStrength = 1f;
                light.shadowNearPlane = 0.1f;
                light.shadowBias = 0.439f;
                light.shadowNormalBias = 0.878f;
                light.useColorTemperature = false;
                light.cullingMask = 1;
                var additionalData = light.GetUniversalAdditionalLightData();
                additionalData.usePipelineSettings = false;
                additionalData.softShadowQuality = SoftShadowQuality.Medium;
                additionalData.renderingLayers = 1;
                additionalData.customShadowLayers = true;
                additionalData.shadowRenderingLayers = 1;
                additionalData.lightCookieSize = Vector2.one;
                var lightData = new SerializedObject(additionalData);
                lightData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom;
                lightData.ApplyModifiedPropertiesWithoutUndo();
                var serializedLight = new SerializedObject(light);
                serializedLight.FindProperty("m_Shadows.m_Resolution").intValue = VirtualLightBenchmarkScenarios.ShadowResolution;
                serializedLight.FindProperty("m_UseViewFrustumForShadowCasterCull").boolValue = false;
                serializedLight.ApplyModifiedPropertiesWithoutUndo();
                light.enabled = false;
                lights[index] = light;
            }
            return lights;
        }

        private static VirtualLight[] CreateVirtualLights(Transform parent)
        {
            var root = CreateChild(parent, "Virtual Rig");
            var lights = new VirtualLight[LightCapacity];
            for (var index = 0; index < lights.Length; index++)
            {
                var lightObject = CreateChild(root.transform, $"Virtual Light {index + 1:D3}");
                SetLightTransform(lightObject.transform, index);
                var light = lightObject.AddComponent<VirtualLight>();
                light.Type = VirtualLightType.Spot;
                light.Color = new Color(1f, 0.92f, 0.82f);
                light.Intensity = LightIntensity;
                light.Range = LightRange;
                light.OuterAngle = OuterSpotAngle;
                light.InnerAngle = InnerSpotAngle;
                light.SpotPenumbraSharpness = 0f;
                light.CastShadow = false;
                light.enabled = false;
                lights[index] = light;
            }
            return lights;
        }

        private static void SetLightTransform(Transform transform, int index)
        {
            if (index < VirtualLightBenchmarkScenarios.MaximumComparableShadowedSpotCount)
            {
                var angle = index / (float)VirtualLightBenchmarkScenarios.MaximumComparableShadowedSpotCount * Mathf.PI * 2f;
                transform.position = new Vector3(Mathf.Sin(angle) * 1.5f, 6f, -1f + Mathf.Cos(angle));
            }
            else
            {
                var gridIndex = index - VirtualLightBenchmarkScenarios.MaximumComparableShadowedSpotCount;
                transform.position = new Vector3((gridIndex % 12 - 5.5f) * 0.85f, 4f + (gridIndex % 3) * 0.25f, (gridIndex / 12) * 0.8f - 0.5f);
            }
            transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.25f, 3f) - transform.position, Vector3.up);
        }

        private static Camera CreateCamera(Transform parent)
        {
            var cameraObject = CreateChild(parent, "Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 5.2f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.1f, 3.2f) - cameraObject.transform.position, Vector3.up);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.012f, 0.025f);
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetTexture = null;
            return camera;
        }

        private static GameObject CreatePrimitive(Transform parent, string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, ShadowCastingMode shadowCastingMode, bool receiveShadows)
        {
            var value = GameObject.CreatePrimitive(primitiveType);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.position = position;
            value.transform.localScale = scale;
            var collider = value.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = value.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadowCastingMode;
            renderer.receiveShadows = receiveShadows;
            renderer.renderingLayerMask = 1;
            return value;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void AssignControllerReferences(VirtualLightBenchmarkController controller, Light[] standardLights, VirtualLight[] virtualLights, GameObject standardReceivers, GameObject virtualReceivers, GameObject standardCasters, GameObject virtualCasters, Camera camera)
        {
            var serializedController = new SerializedObject(controller);
            AssignArray(serializedController.FindProperty("standardLights"), standardLights.Cast<UnityEngine.Object>().ToArray());
            AssignArray(serializedController.FindProperty("virtualLights"), virtualLights.Cast<UnityEngine.Object>().ToArray());
            serializedController.FindProperty("standardReceivers").objectReferenceValue = standardReceivers;
            serializedController.FindProperty("virtualReceivers").objectReferenceValue = virtualReceivers;
            serializedController.FindProperty("standardCasters").objectReferenceValue = standardCasters;
            serializedController.FindProperty("virtualCasters").objectReferenceValue = virtualCasters;
            serializedController.FindProperty("benchmarkCamera").objectReferenceValue = camera;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void AddBenchmarkSceneFirst()
        {
            var benchmarkScene = new EditorBuildSettingsScene(ScenePath, true);
            var scenes = EditorBuildSettings.scenes.Where(value => value.path != ScenePath).ToList();
            scenes.Insert(0, benchmarkScene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            var current = "Assets";
            var parts = path.Split('/');
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
