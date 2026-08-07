using System;
using System.Linq;
using MizoTake.VirtualLight.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MizoTake.VirtualLight.Tests
{
    public sealed class VirtualLightComponentConverterTests
    {
        private Scene previousActiveScene;
        private Scene testScene;
        private bool ownsTestScene;

        [SetUp]
        public void SetUp()
        {
            Selection.objects = Array.Empty<Object>();
            previousActiveScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(previousActiveScene.path))
            {
                testScene = previousActiveScene;
                ownsTestScene = false;
                return;
            }
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(testScene);
            ownsTestScene = true;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = Array.Empty<Object>();
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded) SceneManager.SetActiveScene(previousActiveScene);
            if (ownsTestScene && testScene.IsValid() && testScene.isLoaded) EditorSceneManager.CloseScene(testScene, true);
        }

        [Test]
        public void ConvertLight_PreservesPointParametersAndGameObject()
        {
            var gameObject = new GameObject("Point Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Point;
                source.color = new Color(0.2f, 0.4f, 0.8f, 1f);
                source.intensity = 3.75f;
                source.range = 12.5f;
                source.shadows = LightShadows.Soft;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.gameObject, Is.SameAs(gameObject));
                Assert.That(converted.Type, Is.EqualTo(VirtualLightType.Point));
                Assert.That(converted.Shape, Is.EqualTo(VirtualLightShape.Circle));
                AssertColor(converted.Color, new Color(0.2f, 0.4f, 0.8f, 1f));
                Assert.That(converted.Intensity, Is.EqualTo(3.75f));
                Assert.That(converted.Range, Is.EqualTo(12.5f));
                Assert.That(converted.CastShadow, Is.True);
                Assert.That(gameObject.GetComponent<Light>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_PreservesSpotAnglesAndDisabledState()
        {
            var gameObject = new GameObject("Spot Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Spot;
                source.innerSpotAngle = 18f;
                source.spotAngle = 47f;
                source.enabled = false;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.Type, Is.EqualTo(VirtualLightType.Spot));
                Assert.That(converted.Shape, Is.EqualTo(VirtualLightShape.Circle));
                Assert.That(converted.InnerAngle, Is.EqualTo(18f));
                Assert.That(converted.OuterAngle, Is.EqualTo(47f));
                Assert.That(converted.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(LightType.Spot)]
        [TestCase(LightType.Directional)]
        public void ConvertLight_PreservesTwoDimensionalGoboCookie(LightType lightType)
        {
            var gameObject = new GameObject(lightType + " Gobo Light");
            var source = gameObject.AddComponent<Light>();
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            try
            {
                source.type = lightType;
                source.cookie = texture;
                source.cookieSize = 17f;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.GoboTexture, Is.SameAs(texture));
                if (lightType == LightType.Directional) Assert.That(converted.Range, Is.EqualTo(17f));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_DoesNotAssignPointCubemapToTwoDimensionalGobo()
        {
            var gameObject = new GameObject("Point Cubemap Cookie Light");
            var source = gameObject.AddComponent<Light>();
            var cubemap = new Cubemap(16, TextureFormat.RGBA32, false);
            try
            {
                source.type = LightType.Point;
                source.cookie = cubemap;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.Type, Is.EqualTo(VirtualLightType.Point));
                Assert.That(converted.GoboTexture, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(cubemap);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_MapsPyramidSpotToRectangleShape()
        {
            var gameObject = new GameObject("Pyramid Spot Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Spot;
                SetPyramidShape(source);
                source.range = 8f;
                source.innerSpotAngle = 20f;
                source.spotAngle = 50f;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.Type, Is.EqualTo(VirtualLightType.Spot));
                Assert.That(converted.Shape, Is.EqualTo(VirtualLightShape.Rectangle));
                Assert.That(converted.Range, Is.EqualTo(8f));
                Assert.That(converted.InnerAngle, Is.EqualTo(20f));
                Assert.That(converted.OuterAngle, Is.EqualTo(50f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_LeavesBoxSpotUnchanged()
        {
            var gameObject = new GameObject("Box Spot Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Spot;
                SetBoxShape(source);

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Null);
                Assert.That(gameObject.GetComponent<Light>(), Is.SameAs(source));
                Assert.That(gameObject.GetComponent<MizoTake.VirtualLight.VirtualLight>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_PreservesSpotInnerAngleAboveVirtualLightDefaults()
        {
            var gameObject = new GameObject("Wide Spot Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Spot;
                source.spotAngle = 112f;
                source.innerSpotAngle = 78f;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.InnerAngle, Is.EqualTo(78f));
                Assert.That(converted.OuterAngle, Is.EqualTo(112f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_PreservesRectangleAreaSize()
        {
            var gameObject = new GameObject("Rectangle Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Rectangle;
                source.areaSize = new Vector2(4.5f, 2.25f);

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.Type, Is.EqualTo(VirtualLightType.RectangleArea));
                Assert.That(converted.AreaSize, Is.EqualTo(new Vector2(4.5f, 2.25f)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_MapsDirectionalWithoutPositionOrRangeApproximation()
        {
            var gameObject = new GameObject("Directional Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                gameObject.transform.position = new Vector3(120f, -30f, 85f);
                gameObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.2f, -0.8f, 0.5f).normalized, Vector3.up);
                source.type = LightType.Directional;
                source.color = new Color(0.6f, 0.75f, 1f, 1f);
                source.intensity = 2.5f;
                source.range = 250f;
                source.shadows = LightShadows.Soft;
                source.enabled = false;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(converted.Type, Is.EqualTo(VirtualLightType.Directional));
                Assert.That(converted.transform.position, Is.EqualTo(new Vector3(120f, -30f, 85f)));
                Assert.That(Vector3.Dot(converted.transform.forward, new Vector3(0.2f, -0.8f, 0.5f).normalized), Is.GreaterThan(0.9999f));
                AssertColor(converted.Color, new Color(0.6f, 0.75f, 1f, 1f));
                Assert.That(converted.Intensity, Is.EqualTo(2.5f));
                Assert.That(converted.Range, Is.Not.EqualTo(250f));
                Assert.That(converted.CastShadow, Is.True);
                Assert.That(converted.enabled, Is.False);
                Assert.That(gameObject.GetComponent<Light>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_BakesEnabledColorTemperatureIntoColor()
        {
            var gameObject = new GameObject("Temperature Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Point;
                source.color = new Color(0.8f, 0.6f, 0.4f, 1f);
                source.useColorTemperature = true;
                source.colorTemperature = 3200f;
                var expected = source.color * Mathf.CorrelatedColorTemperatureToRGB(source.colorTemperature);

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                AssertColor(converted.Color, expected);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_LeavesUnsupportedLightUnchanged()
        {
            var gameObject = new GameObject("Disc Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Disc;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Null);
                Assert.That(gameObject.GetComponent<Light>(), Is.SameAs(source));
                Assert.That(gameObject.GetComponent<MizoTake.VirtualLight.VirtualLight>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_LeavesLightWithUnknownDependentComponentUnchanged()
        {
            var gameObject = new GameObject("Required Light");
            var source = gameObject.AddComponent<Light>();
            var dependent = gameObject.AddComponent<LightDependentTestComponent>();
            try
            {
                source.type = LightType.Point;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Null);
                Assert.That(gameObject.GetComponent<Light>(), Is.SameAs(source));
                Assert.That(gameObject.GetComponent<LightDependentTestComponent>(), Is.SameAs(dependent));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ConvertLight_RemovesUrpAdditionalDataBeforeSourceLight()
        {
            var gameObject = new GameObject("URP Light");
            var source = gameObject.AddComponent<Light>();
            gameObject.AddComponent<UniversalAdditionalLightData>();
            try
            {
                source.type = LightType.Point;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Not.Null);
                Assert.That(gameObject.GetComponent<Light>(), Is.Null);
                Assert.That(gameObject.GetComponent<UniversalAdditionalLightData>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

#pragma warning disable 618
        private static void SetPyramidShape(Light light) => light.shape = LightShape.Pyramid;

        private static void SetBoxShape(Light light) => light.shape = LightShape.Box;
#pragma warning restore 618

        [Test]
        public void ConvertLight_LeavesSourceWhenVirtualLightAlreadyExists()
        {
            var gameObject = new GameObject("Mixed Light");
            var source = gameObject.AddComponent<Light>();
            var existing = gameObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            try
            {
                source.type = LightType.Point;

                var converted = VirtualLightComponentConverter.ConvertLight(source, false);

                Assert.That(converted, Is.Null);
                Assert.That(gameObject.GetComponent<Light>(), Is.SameAs(source));
                Assert.That(gameObject.GetComponent<MizoTake.VirtualLight.VirtualLight>(), Is.SameAs(existing));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FindLightsInCurrentStage_IncludesInactiveLights()
        {
            var gameObject = new GameObject("Inactive Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Point;
                gameObject.SetActive(false);

                Assert.That(VirtualLightComponentConverter.FindLightsInCurrentStage(), Does.Contain(source));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FindLightsInSelection_DeduplicatesGameObjectAndComponentSelection()
        {
            var gameObject = new GameObject("Selected Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Point;
                Selection.objects = new Object[] { gameObject, source };

                var lights = VirtualLightComponentConverter.FindLightsInSelection();

                Assert.That(lights.Count(light => light == source), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FindLightsInSelection_DoesNotIncludeChildrenImplicitly()
        {
            var parent = new GameObject("Selected Parent");
            var child = new GameObject("Child Light");
            child.transform.SetParent(parent.transform);
            var source = child.AddComponent<Light>();
            try
            {
                source.type = LightType.Point;
                Selection.objects = new Object[] { parent };

                Assert.That(VirtualLightComponentConverter.FindLightsInSelection().Any(light => light == source), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ConvertLights_RegistersSingleUndoForReplacement()
        {
            var gameObject = new GameObject("Undo Light");
            var source = gameObject.AddComponent<Light>();
            try
            {
                source.type = LightType.Point;
                source.intensity = 5.5f;

                var result = VirtualLightComponentConverter.ConvertLights(new[] { source });

                Assert.That(result.ConvertedCount, Is.EqualTo(1));
                Assert.That(gameObject.GetComponent<Light>(), Is.Null);
                Assert.That(gameObject.GetComponent<MizoTake.VirtualLight.VirtualLight>().Intensity, Is.EqualTo(5.5f));

                Undo.PerformUndo();

                var restored = gameObject.GetComponent<Light>();
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.intensity, Is.EqualTo(5.5f));
                Assert.That(gameObject.GetComponent<MizoTake.VirtualLight.VirtualLight>(), Is.Null);

                Undo.PerformRedo();

                Assert.That(gameObject.GetComponent<Light>(), Is.Null);
                Assert.That(gameObject.GetComponent<MizoTake.VirtualLight.VirtualLight>().Intensity, Is.EqualTo(5.5f));
            }
            finally
            {
                Undo.ClearUndo(gameObject);
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(Vector4.Distance(actual, expected), Is.LessThan(0.0001f));
        }
    }

    [RequireComponent(typeof(Light))]
    public sealed class LightDependentTestComponent : MonoBehaviour
    {
    }
}
