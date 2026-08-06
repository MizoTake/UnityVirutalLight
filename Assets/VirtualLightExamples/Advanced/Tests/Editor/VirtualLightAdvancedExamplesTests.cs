using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MizoTake.VirtualLight.AdvancedExamples.Tests
{
    public sealed class VirtualLightAdvancedExamplesTests
    {
        [TestCase("Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightFeatureLab.unity", "Virtual Light Feature Lab")]
        [TestCase("Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightArenaSample.unity", "Virtual Light Arena Sample")]
        [TestCase("Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightAreaDirectionSample.unity", "Virtual Light Area Direction Sample")]
        public void AdvancedScene_LoadsWithoutMissingComponents(string scenePath, string expectedRootName)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.GetRootGameObjects().Select(root => root.name), Does.Contain(expectedRootName));
            var missingOwners = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Where(transform => transform.GetComponents<Component>().Any(component => component == null)).Select(transform => transform.name).ToArray();
            Assert.That(missingOwners, Is.Empty);
        }

        [Test]
        public void AreaDirectionScene_ContrastsForwardBackFaceAndTwoSidedEmission()
        {
            EditorSceneManager.OpenScene("Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightAreaDirectionSample.unity", OpenSceneMode.Single);
            var areaLights = Object.FindObjectsByType<MizoTake.VirtualLight.VirtualLight>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(light => light.Type == VirtualLightType.RectangleArea).OrderBy(light => light.name).ToArray();
            Assert.That(areaLights.Select(light => light.name), Is.EqualTo(new[] { "Back-Facing One-Sided Area", "Back-Facing Two-Sided Area", "Forward-Facing One-Sided Area" }));
            var backFacingOneSided = areaLights.Single(light => light.name == "Back-Facing One-Sided Area");
            var backFacingTwoSided = areaLights.Single(light => light.name == "Back-Facing Two-Sided Area");
            var forwardFacingOneSided = areaLights.Single(light => light.name == "Forward-Facing One-Sided Area");
            Assert.That(Vector3.Dot(forwardFacingOneSided.transform.forward, Vector3.down), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(backFacingOneSided.transform.forward, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(backFacingTwoSided.transform.forward, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(forwardFacingOneSided.TwoSided, Is.False);
            Assert.That(backFacingOneSided.TwoSided, Is.False);
            Assert.That(backFacingTwoSided.TwoSided, Is.True);
            Assert.That(areaLights.Select(light => light.Intensity).Distinct().Count(), Is.EqualTo(1));
            Assert.That(areaLights.Select(light => light.Range).Distinct().Count(), Is.EqualTo(1));
            Assert.That(areaLights.Select(light => light.AreaSize).Distinct().Count(), Is.EqualTo(1));
            Assert.That(areaLights.Select(light => light.AreaSampleCount).Distinct().Count(), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        }

        [Test]
        public void FeatureLab_UsesThinAnalyticBeamImpactWithBoundedRefresh()
        {
            EditorSceneManager.OpenScene("Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightFeatureLab.unity", OpenSceneMode.Single);
            var occlusion = Object.FindFirstObjectByType<VirtualLightBeamOcclusion>(FindObjectsInactive.Include);

            Assert.That(occlusion, Is.Not.Null);
            Assert.That(occlusion.ImpactVisual, Is.Not.Null);
            Assert.That(occlusion.ImpactVisual.name, Is.EqualTo("Beam Impact - Analytic Footprint"));
            Assert.That(occlusion.FitImpactToSpotCone, Is.True);
            Assert.That(occlusion.MaximumImpactAspectRatio, Is.EqualTo(8f).Within(0.001f));
            Assert.That(occlusion.MaximumRefreshRate, Is.EqualTo(60f).Within(0.001f));
            Assert.That(occlusion.ImpactVisual.localScale.z, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(occlusion.ImpactVisual.GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo("Quad"));
            Assert.That(occlusion.ImpactVisual.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("Mizot/Virtual Light/Impact Footprint"));
        }

        [Test]
        public void Arena_SpotHotspotMatchesVolumetricBeamCore()
        {
            EditorSceneManager.OpenScene("Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightArenaSample.unity", OpenSceneMode.Single);
            var spotLights = Object.FindObjectsByType<MizoTake.VirtualLight.VirtualLight>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(light => light.Type == VirtualLightType.Spot).ToArray();

            Assert.That(spotLights, Has.Length.EqualTo(6));
            foreach (var spotLight in spotLights)
            {
                var beamVolume = spotLight.GetComponentInChildren<VirtualLightBeamVolume>(true);
                Assert.That(beamVolume, Is.Not.Null, spotLight.name);
                var beamMaterial = beamVolume.GetComponent<Renderer>().sharedMaterial;
                Assert.That(beamMaterial, Is.Not.Null, spotLight.name);
                Assert.That(beamMaterial.HasProperty("_CoreRadius"), Is.True, spotLight.name);
                var outerRadius = VirtualLightMath.EvaluateBeamRadius(1f, spotLight.OuterAngle);
                var hotspotRadiusRatio = VirtualLightMath.EvaluateBeamRadius(1f, spotLight.InnerAngle) / outerRadius;
                Assert.That(hotspotRadiusRatio, Is.EqualTo(beamMaterial.GetFloat("_CoreRadius")).Within(0.01f), spotLight.name);
                Assert.That(spotLight.SpotPenumbraSharpness, Is.EqualTo(1f).Within(0.001f), spotLight.name);
            }
        }
    }
}
