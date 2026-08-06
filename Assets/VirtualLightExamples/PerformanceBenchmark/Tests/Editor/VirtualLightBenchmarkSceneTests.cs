using System.Linq;
using MizoTake.VirtualLight.PerformanceBenchmark.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MizoTake.VirtualLight.PerformanceBenchmark.Tests
{
    public sealed class VirtualLightBenchmarkSceneTests
    {
        [Test]
        public void BenchmarkSceneHasCompleteEquivalentLightRigs()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(VirtualLightBenchmarkSceneBuilder.ScenePath), Is.Not.Null);
            EditorSceneManager.OpenScene(VirtualLightBenchmarkSceneBuilder.ScenePath, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<VirtualLightBenchmarkController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.StandardLights, Has.Length.EqualTo(128));
            Assert.That(controller.VirtualLights, Has.Length.EqualTo(128));
            Assert.That(controller.StandardLights.All(value => value != null), Is.True);
            Assert.That(controller.VirtualLights.All(value => value != null), Is.True);
            Assert.That(controller.BenchmarkCamera.targetTexture, Is.Null);
            Assert.That(controller.StandardCasters, Is.Not.Null);
            Assert.That(controller.VirtualCasters, Is.Not.Null);
            var standardCasterRenderers = controller.StandardCasters.GetComponentsInChildren<Renderer>(true);
            var virtualCasterRenderers = controller.VirtualCasters.GetComponentsInChildren<Renderer>(true);
            Assert.That(standardCasterRenderers, Has.Length.EqualTo(3));
            Assert.That(virtualCasterRenderers, Has.Length.EqualTo(3));
            Assert.That(standardCasterRenderers.All(value => value.sharedMaterial.shader.name == "Universal Render Pipeline/Lit"), Is.True);
            Assert.That(virtualCasterRenderers.All(value => value.sharedMaterial.shader.name == "MizoTake/Virtual Light/Benchmark Receiver"), Is.True);
            for (var index = 0; index < standardCasterRenderers.Length; index++) Assert.That(standardCasterRenderers[index].transform.position, Is.EqualTo(virtualCasterRenderers[index].transform.position));
            for (var index = 0; index < VirtualLightBenchmarkScenarios.MaximumComparableShadowedSpotCount; index++)
            {
                var standard = controller.StandardLights[index];
                var virtualLight = controller.VirtualLights[index];
                Assert.That(standard.transform.position, Is.EqualTo(virtualLight.transform.position));
                Assert.That(Quaternion.Angle(standard.transform.rotation, virtualLight.transform.rotation), Is.LessThan(0.001f));
                Assert.That(standard.range, Is.EqualTo(virtualLight.Range));
                Assert.That(standard.innerSpotAngle, Is.EqualTo(virtualLight.InnerAngle));
                Assert.That(standard.spotAngle, Is.EqualTo(virtualLight.OuterAngle));
                Assert.That(virtualLight.SpotPenumbraSharpness, Is.Zero);
                var additionalData = standard.GetUniversalAdditionalLightData();
                Assert.That(additionalData.additionalLightsShadowResolutionTier, Is.EqualTo(UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom));
                Assert.That(additionalData.softShadowQuality, Is.EqualTo(SoftShadowQuality.Medium));
                Assert.That(additionalData.usePipelineSettings, Is.False);
                Assert.That(new SerializedObject(standard).FindProperty("m_Shadows.m_Resolution").intValue, Is.EqualTo(VirtualLightBenchmarkScenarios.ShadowResolution));
            }
        }

        [Test]
        public void BenchmarkSceneAndMaterialArePlayerReachable()
        {
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(VirtualLightBenchmarkSceneBuilder.ScenePath));
            Assert.That(PlayerSettings.enableFrameTimingStats, Is.True);
            var material = AssetDatabase.LoadAssetAtPath<Material>(VirtualLightBenchmarkSceneBuilder.VirtualMaterialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("MizoTake/Virtual Light/Benchmark Receiver"));
        }

        [Test]
        public void BenchmarkSceneHasNoMissingComponents()
        {
            EditorSceneManager.OpenScene(VirtualLightBenchmarkSceneBuilder.ScenePath, OpenSceneMode.Single);
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
            }
        }

    }
}
