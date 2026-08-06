using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MizoTake.VirtualLight.Tests
{
    public sealed class VirtualLightDataTests
    {
        [Test]
        public void GpuData_HasSpecifiedEightyByteLayout()
        {
            Assert.That(Marshal.SizeOf<VirtualLightGpu>(), Is.EqualTo(80));
            Assert.That(Marshal.OffsetOf<VirtualLightGpu>(nameof(VirtualLightGpu.PositionRadius)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<VirtualLightGpu>(nameof(VirtualLightGpu.ColorIntensity)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<VirtualLightGpu>(nameof(VirtualLightGpu.DirectionType)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<VirtualLightGpu>(nameof(VirtualLightGpu.ConeShadowFlags)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<VirtualLightGpu>(nameof(VirtualLightGpu.AreaSizeParams)).ToInt32(), Is.EqualTo(64));
        }

        [Test]
        public void GpuData_LeavesShadowSliceUnassignedUntilCameraSelection()
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.OcclusionDistance = 2.5f;

            var gpu = VirtualLightGpu.FromDescriptor(in descriptor);

            Assert.That(gpu.ConeShadowFlags.z, Is.EqualTo(-1f));
        }

        [Test]
        public void GpuData_SpotPacksPenumbraSharpnessWithoutChangingAreaLightDimensions()
        {
            var spotDescriptor = VirtualLightDescriptor.Default;
            spotDescriptor.Type = VirtualLightType.Spot;
            spotDescriptor.SpotPenumbraSharpness = 0.75f;
            var areaDescriptor = VirtualLightDescriptor.Default;
            areaDescriptor.Type = VirtualLightType.RectangleArea;
            areaDescriptor.AreaSize = new Vector2(4f, 2f);
            areaDescriptor.SpotPenumbraSharpness = 0.75f;

            var spotGpu = VirtualLightGpu.FromDescriptor(in spotDescriptor);
            var areaGpu = VirtualLightGpu.FromDescriptor(in areaDescriptor);

            Assert.That(spotGpu.AreaSizeParams.x, Is.EqualTo(0.75f));
            Assert.That(areaGpu.AreaSizeParams.x, Is.EqualTo(4f));
            Assert.That(areaGpu.AreaSizeParams.y, Is.EqualTo(2f));
        }

        [Test]
        public void Descriptor_DirectionIncludesParentReflection()
        {
            var parent = new GameObject("Reflected Parent");
            var lightObject = new GameObject("Virtual Light");
            try
            {
                parent.transform.localScale = new Vector3(-1f, 1f, 1f);
                lightObject.transform.SetParent(parent.transform, false);
                lightObject.transform.localRotation = Quaternion.LookRotation(new Vector3(1f, 0f, 1f).normalized, Vector3.up);
                var virtualLight = lightObject.AddComponent<VirtualLight>();
                var expectedDirection = lightObject.transform.localToWorldMatrix.MultiplyVector(Vector3.forward).normalized;

                Assert.That(virtualLight.Descriptor.Direction.x, Is.EqualTo(expectedDirection.x).Within(0.0001f));
                Assert.That(virtualLight.Descriptor.Direction.y, Is.EqualTo(expectedDirection.y).Within(0.0001f));
                Assert.That(virtualLight.Descriptor.Direction.z, Is.EqualTo(expectedDirection.z).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Component_BindsFallbackShadowBuffersBeforeFirstCameraRender()
        {
            var gameObject = new GameObject("Virtual Light");
            try
            {
                gameObject.AddComponent<VirtualLight>();
                Assert.That(VirtualLightRenderBridge.HasLightBinding, Is.True);
                Assert.That(VirtualLightRenderBridge.HasTileBindings, Is.True);
                Assert.That(VirtualLightShadowMapArray.HasBindings, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Inspector_ExposesAffectOpaque()
        {
            var gameObject = new GameObject("Virtual Light");
            UnityEditor.Editor editor = null;
            try
            {
                var virtualLight = gameObject.AddComponent<VirtualLight>();
                editor = UnityEditor.Editor.CreateEditor(virtualLight);
                var field = editor.GetType().GetField("affectOpaque", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(field, Is.Not.Null);
                Assert.That(field.GetValue(editor), Is.Not.Null);
            }
            finally
            {
                if (editor != null) Object.DestroyImmediate(editor);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Inspector_ExposesSpotPenumbraSharpness()
        {
            var gameObject = new GameObject("Virtual Light");
            UnityEditor.Editor editor = null;
            try
            {
                var virtualLight = gameObject.AddComponent<VirtualLight>();
                editor = UnityEditor.Editor.CreateEditor(virtualLight);
                var field = editor.GetType().GetField("spotPenumbraSharpness", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(field, Is.Not.Null);
                Assert.That(field.GetValue(editor), Is.Not.Null);
            }
            finally
            {
                if (editor != null) Object.DestroyImmediate(editor);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Sanitize_RepairsInvalidValuesAndConeOrder()
        {
            var descriptor = new VirtualLightDescriptor
            {
                Position = new Vector3(float.NaN, 2f, float.PositiveInfinity),
                Direction = Vector3.zero,
                LinearColor = new Color(float.NaN, 0.5f, float.PositiveInfinity, 1f),
                Intensity = -4f,
                Radius = float.NaN,
                InnerConeAngle = 70f,
                OuterConeAngle = 20f,
                SpotPenumbraSharpness = 3f,
                AreaSize = new Vector2(0f, float.NaN),
                AreaSampleCount = 3,
                Type = VirtualLightType.RectangleArea,
                Flags = VirtualLightFlags.Enabled | VirtualLightFlags.AffectOpaque
            };

            var sanitized = descriptor.Sanitized();

            Assert.That(sanitized.Position, Is.EqualTo(new Vector3(0f, 2f, 0f)));
            Assert.That(sanitized.Direction, Is.EqualTo(Vector3.forward));
            Assert.That(sanitized.LinearColor.r, Is.Zero);
            Assert.That(sanitized.LinearColor.b, Is.Zero);
            Assert.That(sanitized.Intensity, Is.Zero);
            Assert.That(sanitized.Radius, Is.Zero);
            Assert.That(sanitized.InnerConeAngle, Is.EqualTo(20f));
            Assert.That(sanitized.OuterConeAngle, Is.EqualTo(70f));
            Assert.That(sanitized.SpotPenumbraSharpness, Is.EqualTo(1f));
            Assert.That(sanitized.AreaSize, Is.EqualTo(new Vector2(0.01f, 0.01f)));
            Assert.That(sanitized.AreaSampleCount, Is.EqualTo(4));
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 4)]
        [TestCase(6, 8)]
        [TestCase(12, 16)]
        [TestCase(99, 16)]
        public void Sanitize_UsesSupportedAreaSampleCount(int requested, int expected)
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.RectangleArea;
            descriptor.AreaSampleCount = requested;

            Assert.That(descriptor.Sanitized().AreaSampleCount, Is.EqualTo(expected));
        }

        [Test]
        public void Shaders_ImportWithoutErrorsAndComputeKernelExists()
        {
            var shader = Shader.Find("Mizot/Virtual Light/Lit");
            Assert.That(shader, Is.Not.Null);
            var messages = ShaderUtil.GetShaderMessages(shader);
            Assert.That(messages, Has.None.Matches<ShaderMessage>(message => message.severity.ToString() == "Error"));
            var beamShader = Shader.Find("Mizot/Virtual Light/Beam");
            Assert.That(beamShader, Is.Not.Null);
            var beamMessages = ShaderUtil.GetShaderMessages(beamShader);
            Assert.That(beamMessages, Has.None.Matches<ShaderMessage>(message => message.severity.ToString() == "Error"));
            var impactShader = Shader.Find("Mizot/Virtual Light/Impact Footprint");
            Assert.That(impactShader, Is.Not.Null);
            var impactMessages = ShaderUtil.GetShaderMessages(impactShader);
            Assert.That(impactMessages, Has.None.Matches<ShaderMessage>(message => message.severity.ToString() == "Error"));
            var computeShader = Resources.Load<ComputeShader>("VirtualLightTileCulling");
            Assert.That(computeShader, Is.Not.Null);
        }

        [Test]
        public void PackageSample_IsOneStaticSceneWithoutScripts()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VirtualLightDataTests).Assembly);
            Assert.That(packageInfo, Is.Not.Null);
            var sampleRoot = Path.Combine(packageInfo.resolvedPath, "Samples~", "Basic");
            var scenes = Directory.GetFiles(sampleRoot, "*.unity", SearchOption.AllDirectories);
            var scripts = Directory.GetFiles(sampleRoot, "*.cs", SearchOption.AllDirectories);
            Assert.That(scenes, Has.Length.EqualTo(1));
            Assert.That(Path.GetFileName(scenes[0]), Is.EqualTo("VirtualLightBasicSample.unity"));
            Assert.That(scripts, Is.Empty);
        }

        [Test]
        public void PackageSampleScene_DoesNotSerializeUnityLightComponent()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VirtualLightDataTests).Assembly);
            Assert.That(packageInfo, Is.Not.Null);
            const string sceneName = "VirtualLightBasicSample.unity";
            var scenePath = Path.Combine(packageInfo.resolvedPath, "Samples~", "Basic", "Scenes", sceneName);
            Assert.That(File.Exists(scenePath), Is.True, $"Package sample scene was not found: {scenePath}");

            var serializedScene = File.ReadAllText(scenePath);

            StringAssert.DoesNotContain("--- !u!108 ", serializedScene, $"{sceneName} must not serialize a UnityEngine.Light component.");
        }

        [Test]
        public void PackageSample_DoesNotRequireUgui()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VirtualLightDataTests).Assembly);
            Assert.That(packageInfo, Is.Not.Null);
            var sampleRoot = Path.Combine(packageInfo.resolvedPath, "Samples~", "Basic");
            foreach (var scriptPath in Directory.GetFiles(sampleRoot, "*.cs", SearchOption.AllDirectories)) StringAssert.DoesNotContain("UnityEngine.UI", File.ReadAllText(scriptPath), $"Sample script requires UGUI: {scriptPath}");
            foreach (var scenePath in Directory.GetFiles(sampleRoot, "*.unity", SearchOption.AllDirectories))
            {
                var serializedScene = File.ReadAllText(scenePath);
                StringAssert.DoesNotContain("Canvas:", serializedScene, $"Sample scene requires UGUI Canvas: {scenePath}");
                StringAssert.DoesNotContain("CanvasRenderer:", serializedScene, $"Sample scene requires UGUI CanvasRenderer: {scenePath}");
            }
        }

        [Test]
        public void RuntimeAssembly_DoesNotContainUrpSpotLightBridge()
        {
            Assert.That(typeof(VirtualLightSystem).Assembly.GetType("MizoTake.VirtualLight.VirtualLightUrpSpotBridge", false), Is.Null);
        }

        [TestCase(1, 4f, 2f, 1, 1)]
        [TestCase(2, 4f, 2f, 2, 1)]
        [TestCase(2, 2f, 4f, 1, 2)]
        [TestCase(4, 4f, 2f, 2, 2)]
        [TestCase(8, 4f, 2f, 4, 2)]
        [TestCase(8, 2f, 4f, 2, 4)]
        [TestCase(16, 4f, 2f, 4, 4)]
        public void AreaSampleGrid_IsCenteredAndUsesEveryCell(int sampleCount, float width, float height, int expectedColumns, int expectedRows)
        {
            var grid = VirtualLightMath.GetAreaSampleGrid(sampleCount, new Vector2(width, height));

            Assert.That(grid, Is.EqualTo(new Vector2Int(expectedColumns, expectedRows)));
            Assert.That(grid.x * grid.y, Is.EqualTo(sampleCount));
        }
    }
}
