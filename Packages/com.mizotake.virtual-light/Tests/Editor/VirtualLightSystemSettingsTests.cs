using System.Collections.Generic;
using MizoTake.VirtualLight.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MizoTake.VirtualLight.Tests
{
    public sealed class VirtualLightSystemSettingsTests
    {
        private const string TestFolderPath = "Assets/VirtualLightSystemSettingsTests";
        private const string TestAssetPath = TestFolderPath + "/VirtualLightSystemSettings.asset";

        [SetUp]
        public void SetUp()
        {
            VirtualLightSystem.ResetForTests();
            AssetDatabase.DeleteAsset(TestFolderPath);
        }

        [TearDown]
        public void TearDown()
        {
            VirtualLightSystem.ResetForTests();
            AssetDatabase.DeleteAsset(TestFolderPath);
        }

        [Test]
        public void Settings_DefaultValuesMatchExistingRuntimeBehavior()
        {
            var settings = ScriptableObject.CreateInstance<VirtualLightSystemSettings>();

            Assert.That(settings.Quality, Is.EqualTo(VirtualLightQuality.Medium));
            Assert.That(settings.ShadowDepthBias, Is.EqualTo(0.0015f));
            Assert.That(settings.ShadowNormalBias, Is.EqualTo(0.003f));
            Assert.That(settings.ShadowCasterLayers.value, Is.EqualTo(~0));
            Assert.That(VirtualLightSystemSettings.GetShadowMapResolution(settings.Quality), Is.EqualTo(512));
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void ApplySettings_UpdatesSystemAndShadowSamplingParameters()
        {
            var settings = ScriptableObject.CreateInstance<VirtualLightSystemSettings>();
            settings.Quality = VirtualLightQuality.High;
            settings.ShadowDepthBias = 0.004f;
            settings.ShadowNormalBias = 0.012f;
            settings.ShadowCasterLayers = 1 << 8;

            VirtualLightSystem.Current.ApplySettings(settings);
            var samplingParameters = VirtualLightShadowMapArray.BuildShadowSamplingParameters(768);

            Assert.That(VirtualLightSystem.Quality, Is.EqualTo(VirtualLightQuality.High));
            Assert.That(VirtualLightSystem.ShadowDepthBias, Is.EqualTo(0.004f));
            Assert.That(VirtualLightSystem.ShadowNormalBias, Is.EqualTo(0.012f));
            Assert.That(VirtualLightSystem.ShadowCasterLayerMask, Is.EqualTo(1 << 8));
            Assert.That(samplingParameters, Is.EqualTo(new Vector4(1f / 768f, 1f / 768f, 0.004f, 0.012f)));
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void ApplySettings_SanitizesInvalidBiasValues()
        {
            var settings = ScriptableObject.CreateInstance<VirtualLightSystemSettings>();
            settings.ShadowDepthBias = float.NaN;
            settings.ShadowNormalBias = -1f;

            VirtualLightSystem.Current.ApplySettings(settings);

            Assert.That(VirtualLightSystem.ShadowDepthBias, Is.EqualTo(VirtualLightSystemSettings.DefaultShadowDepthBias));
            Assert.That(VirtualLightSystem.ShadowNormalBias, Is.Zero);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void SetQuality_OverridesOnlyQuality()
        {
            var settings = ScriptableObject.CreateInstance<VirtualLightSystemSettings>();
            settings.Quality = VirtualLightQuality.High;
            settings.ShadowDepthBias = 0.004f;
            settings.ShadowNormalBias = 0.012f;
            settings.ShadowCasterLayers = 1 << 8;
            VirtualLightSystem.Current.ApplySettings(settings);

            VirtualLightSystem.Current.SetQuality(VirtualLightQuality.Ultra);

            Assert.That(VirtualLightSystem.Quality, Is.EqualTo(VirtualLightQuality.Ultra));
            Assert.That(VirtualLightSystem.ShadowDepthBias, Is.EqualTo(0.004f));
            Assert.That(VirtualLightSystem.ShadowNormalBias, Is.EqualTo(0.012f));
            Assert.That(VirtualLightSystem.ShadowCasterLayerMask, Is.EqualTo(1 << 8));
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void ResetForTests_RestoresCodeDefaults()
        {
            var settings = ScriptableObject.CreateInstance<VirtualLightSystemSettings>();
            settings.Quality = VirtualLightQuality.Ultra;
            settings.ShadowDepthBias = 0.5f;
            settings.ShadowNormalBias = 0.25f;
            settings.ShadowCasterLayers = 0;
            VirtualLightSystem.Current.ApplySettings(settings);

            VirtualLightSystem.ResetForTests();

            Assert.That(VirtualLightSystem.Quality, Is.EqualTo(VirtualLightQuality.Medium));
            Assert.That(VirtualLightSystem.ShadowDepthBias, Is.EqualTo(VirtualLightSystemSettings.DefaultShadowDepthBias));
            Assert.That(VirtualLightSystem.ShadowNormalBias, Is.EqualTo(VirtualLightSystemSettings.DefaultShadowNormalBias));
            Assert.That(VirtualLightSystem.ShadowCasterLayerMask, Is.EqualTo(VirtualLightSystemSettings.DefaultShadowCasterLayerMask));
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void ShadowCasterLayers_FilterRegisteredOccluderRenderers()
        {
            var settings = ScriptableObject.CreateInstance<VirtualLightSystemSettings>();
            settings.ShadowCasterLayers = 1 << 8;
            VirtualLightSystem.Current.ApplySettings(settings);
            var included = GameObject.CreatePrimitive(PrimitiveType.Cube);
            included.layer = 8;
            included.AddComponent<VirtualLightOccluder>();
            var excluded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            excluded.layer = 9;
            excluded.AddComponent<VirtualLightOccluder>();
            var renderers = new List<Renderer>();

            VirtualLightOccluder.CollectShadowRenderers(renderers);

            CollectionAssert.Contains(renderers, included.GetComponent<Renderer>());
            CollectionAssert.DoesNotContain(renderers, excluded.GetComponent<Renderer>());
            Object.DestroyImmediate(included);
            Object.DestroyImmediate(excluded);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CreateSettingsAssetAtPath_CreatesPersistentDefaultAsset()
        {
            var settings = VirtualLightSystemSettingsWindow.CreateSettingsAssetAtPath(TestAssetPath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<VirtualLightSystemSettings>(TestAssetPath), Is.SameAs(settings));
            Assert.That(settings.Quality, Is.EqualTo(VirtualLightQuality.Medium));
        }

        [Test]
        public void SettingsAssetPath_MatchesRuntimeResourcesPath()
        {
            Assert.That(VirtualLightSystemSettingsWindow.SettingsAssetPath, Is.EqualTo("Assets/Resources/" + VirtualLightSystemSettings.ResourcePath + ".asset"));
        }

        [Test]
        public void CreateSettingsAssetAtPath_RejectsSiblingDirectoryNamedLikeAssets()
        {
            const string invalidPath = "AssetsOutside/VirtualLightSystemSettings.asset";
            LogAssert.Expect(LogType.Error, $"Virtual Light system settings must be created under Assets. Requested path: '{invalidPath}'.");

            var settings = VirtualLightSystemSettingsWindow.CreateSettingsAssetAtPath(invalidPath);

            Assert.That(settings, Is.Null);
        }
    }
}
