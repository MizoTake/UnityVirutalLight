using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
            spotDescriptor.Shape = VirtualLightShape.Rectangle;
            spotDescriptor.SpotPenumbraSharpness = 0.75f;
            spotDescriptor.AreaRotation = 30f;
            var areaDescriptor = VirtualLightDescriptor.Default;
            areaDescriptor.Type = VirtualLightType.RectangleArea;
            areaDescriptor.AreaSize = new Vector2(4f, 2f);
            areaDescriptor.SpotPenumbraSharpness = 0.75f;

            var spotGpu = VirtualLightGpu.FromDescriptor(in spotDescriptor);
            var areaGpu = VirtualLightGpu.FromDescriptor(in areaDescriptor);

            Assert.That(spotGpu.AreaSizeParams.x, Is.EqualTo(0.75f));
            Assert.That(spotGpu.AreaSizeParams.y, Is.EqualTo((float)VirtualLightShape.Rectangle));
            Assert.That(spotGpu.AreaSizeParams.w, Is.EqualTo(30f * Mathf.Deg2Rad).Within(0.0001f));
            Assert.That(areaGpu.AreaSizeParams.x, Is.EqualTo(4f));
            Assert.That(areaGpu.AreaSizeParams.y, Is.EqualTo(2f));
        }

        [Test]
        public void GpuData_PointPacksShapeAndRotationWithoutChangingLayout()
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Point;
            descriptor.Shape = VirtualLightShape.Rectangle;
            descriptor.AreaRotation = -45f;

            var gpu = VirtualLightGpu.FromDescriptor(in descriptor);

            Assert.That(gpu.AreaSizeParams.x, Is.Zero);
            Assert.That(gpu.AreaSizeParams.y, Is.EqualTo((float)VirtualLightShape.Rectangle));
            Assert.That(gpu.AreaSizeParams.w, Is.EqualTo(-45f * Mathf.Deg2Rad).Within(0.0001f));
            Assert.That(Marshal.SizeOf<VirtualLightGpu>(), Is.EqualTo(80));
        }

        [Test]
        public void GpuData_DirectionalPacksDirectionAndTypeWithoutChangingLayout()
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Directional;
            descriptor.Direction = new Vector3(0.25f, -0.5f, 0.75f).normalized;
            descriptor.Radius = 0f;

            var gpu = VirtualLightGpu.FromDescriptor(in descriptor);

            Assert.That(gpu.DirectionType.x, Is.EqualTo(descriptor.Direction.x).Within(0.0001f));
            Assert.That(gpu.DirectionType.y, Is.EqualTo(descriptor.Direction.y).Within(0.0001f));
            Assert.That(gpu.DirectionType.z, Is.EqualTo(descriptor.Direction.z).Within(0.0001f));
            Assert.That(gpu.DirectionType.w, Is.EqualTo((float)VirtualLightType.Directional));
            Assert.That(gpu.PositionRadius.w, Is.Zero);
            Assert.That(Marshal.SizeOf<VirtualLightGpu>(), Is.EqualTo(80));
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
        public void Inspector_ExposesPunctualShape()
        {
            var gameObject = new GameObject("Virtual Light");
            UnityEditor.Editor editor = null;
            try
            {
                var virtualLight = gameObject.AddComponent<VirtualLight>();
                editor = UnityEditor.Editor.CreateEditor(virtualLight);
                var field = editor.GetType().GetField("shape", BindingFlags.Instance | BindingFlags.NonPublic);

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
        public void Inspector_ExposesGoboTexture()
        {
            var gameObject = new GameObject("Virtual Light");
            UnityEditor.Editor editor = null;
            try
            {
                var virtualLight = gameObject.AddComponent<VirtualLight>();
                editor = UnityEditor.Editor.CreateEditor(virtualLight);
                var field = editor.GetType().GetField("goboTexture", BindingFlags.Instance | BindingFlags.NonPublic);

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
        public void Component_GoboTextureSynchronizesToDescriptor()
        {
            var gameObject = new GameObject("Virtual Light Gobo");
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            try
            {
                var virtualLight = gameObject.AddComponent<VirtualLight>();
                virtualLight.GoboTexture = texture;

                Assert.That(virtualLight.GoboTexture, Is.SameAs(texture));
                Assert.That(virtualLight.Descriptor.GoboTexture, Is.SameAs(texture));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GoboArray_BindsFallbackGpuResourcesAtOneHundredTwentyEightPixels()
        {
            VirtualLightGoboArray.EnsureBindings();

            Assert.That(VirtualLightGoboArray.HasBindings, Is.True);
            Assert.That(VirtualLightGoboArray.HasTextureBinding, Is.True);
            Assert.That(VirtualLightGoboArray.Resolution, Is.EqualTo(128));
        }

        [Test]
        public void GoboArray_UnmaskedFallbackAllocatesOneParameterForEverySelectedLight()
        {
            VirtualLightGoboArray.BindUnmasked(65);
            var parametersField = typeof(VirtualLightGoboArray).GetField("parameters", BindingFlags.Static | BindingFlags.NonPublic);
            var parameterCapacityField = typeof(VirtualLightGoboArray).GetField("parameterCapacity", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(parametersField, Is.Not.Null);
            Assert.That(parameterCapacityField, Is.Not.Null);
            var fallbackParameters = (Vector4[])parametersField.GetValue(null);

            Assert.That((int)parameterCapacityField.GetValue(null), Is.GreaterThanOrEqualTo(65));
            Assert.That(fallbackParameters.Take(65).All(parameter => parameter.x == -1f), Is.True);
        }

        [Test]
        public void GoboArray_UploadDeduplicatesTexturesAndLeavesMissingSlicesUnassigned()
        {
            var first = new Texture2D(8, 16, TextureFormat.RGBA32, false, true);
            var second = new Texture2D(256, 64, TextureFormat.RGBA32, false, true);
            try
            {
                first.SetPixels(Enumerable.Repeat(Color.red, first.width * first.height).ToArray());
                first.Apply(false, false);
                second.SetPixels(Enumerable.Repeat(Color.green, second.width * second.height).ToArray());
                second.Apply(false, false);
                var descriptors = new[] { VirtualLightDescriptor.Default, VirtualLightDescriptor.Default, VirtualLightDescriptor.Default, VirtualLightDescriptor.Default };
                descriptors[0].GoboTexture = first;
                descriptors[1].GoboTexture = first;
                descriptors[2].GoboTexture = second;

                VirtualLightGoboArray.Upload(descriptors, descriptors.Length);
                var parametersField = typeof(VirtualLightGoboArray).GetField("parameters", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(parametersField, Is.Not.Null);
                var uploadedParameters = (Vector4[])parametersField.GetValue(null);

                Assert.That(uploadedParameters[0].x, Is.EqualTo(0f));
                Assert.That(uploadedParameters[1].x, Is.EqualTo(0f));
                Assert.That(uploadedParameters[2].x, Is.EqualTo(1f));
                Assert.That(uploadedParameters[3].x, Is.EqualTo(-1f));
                Assert.That(VirtualLightGoboArray.Resolution, Is.EqualTo(128));
                var textureArrayField = typeof(VirtualLightGoboArray).GetField("goboTextures", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(textureArrayField, Is.Not.Null);
                var textureArray = (RenderTexture)textureArrayField.GetValue(null);
                Assert.That(textureArray.width, Is.EqualTo(128));
                Assert.That(textureArray.height, Is.EqualTo(128));
                Assert.That(textureArray.volumeDepth, Is.GreaterThanOrEqualTo(2));
                var previousActive = RenderTexture.active;
                var readback = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
                try
                {
                    Graphics.SetRenderTarget(textureArray, 0, CubemapFace.Unknown, 0);
                    readback.ReadPixels(new Rect(64f, 64f, 1f, 1f), 0, 0);
                    readback.Apply(false, false);
                    Assert.That(readback.GetPixel(0, 0).r, Is.GreaterThan(0.9f));
                    Graphics.SetRenderTarget(textureArray, 0, CubemapFace.Unknown, 1);
                    readback.ReadPixels(new Rect(64f, 64f, 1f, 1f), 0, 0);
                    readback.Apply(false, false);
                    Assert.That(readback.GetPixel(0, 0).g, Is.GreaterThan(0.9f));
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    Object.DestroyImmediate(readback);
                }
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void BeamAndImpactShaders_ExposePerRendererGoboProperties()
        {
            var beamShader = Shader.Find("MizoTake/Virtual Light/Beam");
            var impactShader = Shader.Find("MizoTake/Virtual Light/Impact Footprint");
            Assert.That(beamShader, Is.Not.Null);
            Assert.That(impactShader, Is.Not.Null);
            var beamMaterial = new Material(beamShader);
            var impactMaterial = new Material(impactShader);
            try
            {
                Assert.That(beamMaterial.HasProperty("_VirtualLightGoboTexture"), Is.True);
                Assert.That(beamMaterial.HasProperty("_VirtualLightGoboEnabled"), Is.True);
                Assert.That(impactMaterial.HasProperty("_VirtualLightGoboTexture"), Is.True);
                Assert.That(impactMaterial.HasProperty("_VirtualLightGoboEnabled"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(beamMaterial);
                Object.DestroyImmediate(impactMaterial);
            }
        }

        [Test]
        public void BeamAndImpactComponents_PropagateTheOwningLightsGoboTexture()
        {
            var lightObject = new GameObject("Virtual Light Gobo Visuals");
            var beamObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var impactObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            var beamMaterial = new Material(Shader.Find("MizoTake/Virtual Light/Beam"));
            var impactMaterial = new Material(Shader.Find("MizoTake/Virtual Light/Impact Footprint"));
            try
            {
                beamObject.transform.SetParent(lightObject.transform, false);
                impactObject.transform.SetParent(lightObject.transform, false);
                beamObject.GetComponent<Renderer>().sharedMaterial = beamMaterial;
                impactObject.GetComponent<Renderer>().sharedMaterial = impactMaterial;
                var virtualLight = lightObject.AddComponent<VirtualLight>();
                virtualLight.Type = VirtualLightType.Spot;
                virtualLight.GoboTexture = texture;
                var beamVolume = beamObject.AddComponent<VirtualLightBeamVolume>();
                VirtualLightBeamVolume.ApplyShadowSlices(new[] { virtualLight.Handle }, new[] { VirtualLightGpu.FromDescriptor(virtualLight.Descriptor) }, 1);
                var beamBlock = new MaterialPropertyBlock();
                beamObject.GetComponent<Renderer>().GetPropertyBlock(beamBlock);

                var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
                beamOcclusion.ImpactVisual = impactObject.transform;
                var updateImpactProperties = typeof(VirtualLightBeamOcclusion).GetMethod("UpdateImpactMaterialProperties", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(updateImpactProperties, Is.Not.Null);
                updateImpactProperties.Invoke(beamOcclusion, null);
                var impactBlock = new MaterialPropertyBlock();
                impactObject.GetComponent<Renderer>().GetPropertyBlock(impactBlock);

                Assert.That(beamBlock.GetTexture(Shader.PropertyToID("_VirtualLightGoboTexture")), Is.SameAs(texture));
                Assert.That(beamBlock.GetFloat(Shader.PropertyToID("_VirtualLightGoboEnabled")), Is.EqualTo(1f));
                Assert.That(impactBlock.GetTexture(Shader.PropertyToID("_VirtualLightGoboTexture")), Is.SameAs(texture));
                Assert.That(impactBlock.GetFloat(Shader.PropertyToID("_VirtualLightGoboEnabled")), Is.EqualTo(1f));
                Assert.That(beamVolume, Is.Not.Null);

                virtualLight.GoboTexture = null;
                VirtualLightBeamVolume.ApplyShadowSlices(new[] { virtualLight.Handle }, new[] { VirtualLightGpu.FromDescriptor(virtualLight.Descriptor) }, 1);
                updateImpactProperties.Invoke(beamOcclusion, null);
                beamObject.GetComponent<Renderer>().GetPropertyBlock(beamBlock);
                impactObject.GetComponent<Renderer>().GetPropertyBlock(impactBlock);

                Assert.That(beamBlock.GetTexture(Shader.PropertyToID("_VirtualLightGoboTexture")), Is.SameAs(Texture2D.whiteTexture));
                Assert.That(beamBlock.GetFloat(Shader.PropertyToID("_VirtualLightGoboEnabled")), Is.Zero);
                Assert.That(impactBlock.GetTexture(Shader.PropertyToID("_VirtualLightGoboTexture")), Is.SameAs(Texture2D.whiteTexture));
                Assert.That(impactBlock.GetFloat(Shader.PropertyToID("_VirtualLightGoboEnabled")), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(beamMaterial);
                Object.DestroyImmediate(impactMaterial);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(lightObject);
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
                Shape = (VirtualLightShape)999,
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
            Assert.That(sanitized.Shape, Is.EqualTo(VirtualLightShape.Circle));
        }

        [Test]
        public void DescriptorEquality_DetectsShapeChanges()
        {
            var circle = VirtualLightDescriptor.Default;
            var rectangle = circle;
            rectangle.Shape = VirtualLightShape.Rectangle;

            Assert.That(circle.Equals(rectangle), Is.False);
        }

        [Test]
        public void Component_DefaultsToCircleAndSynchronizesRectangleShape()
        {
            var gameObject = new GameObject("Virtual Light");
            try
            {
                var virtualLight = gameObject.AddComponent<VirtualLight>();

                Assert.That(virtualLight.Shape, Is.EqualTo(VirtualLightShape.Circle));
                virtualLight.Shape = VirtualLightShape.Rectangle;

                Assert.That(virtualLight.Descriptor.Shape, Is.EqualTo(VirtualLightShape.Rectangle));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
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
        public void Sanitize_DirectionalRemainsEnabledWithZeroRadius()
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Directional;
            descriptor.Radius = 0f;

            var sanitized = descriptor.Sanitized();

            Assert.That((sanitized.Flags & VirtualLightFlags.Enabled) != 0, Is.True);
            Assert.That(sanitized.Radius, Is.Zero);
        }

        [Test]
        public void Shaders_ImportWithoutErrorsAndComputeKernelExists()
        {
            var shader = Shader.Find("MizoTake/Virtual Light/Lit");
            Assert.That(shader, Is.Not.Null);
            var messages = ShaderUtil.GetShaderMessages(shader);
            Assert.That(messages, Has.None.Matches<ShaderMessage>(message => message.severity.ToString() == "Error"));
            var beamShader = Shader.Find("MizoTake/Virtual Light/Beam");
            Assert.That(beamShader, Is.Not.Null);
            var beamMessages = ShaderUtil.GetShaderMessages(beamShader);
            Assert.That(beamMessages, Has.None.Matches<ShaderMessage>(message => message.severity.ToString() == "Error"));
            var impactShader = Shader.Find("MizoTake/Virtual Light/Impact Footprint");
            Assert.That(impactShader, Is.Not.Null);
            var impactMessages = ShaderUtil.GetShaderMessages(impactShader);
            Assert.That(impactMessages, Has.None.Matches<ShaderMessage>(message => message.severity.ToString() == "Error"));
            var computeShader = Resources.Load<ComputeShader>("VirtualLightTileCulling");
            Assert.That(computeShader, Is.Not.Null);
        }

        [Test]
        public void LitShader_StandardLightingOptionIsEnabledByDefault()
        {
            var shader = Shader.Find("MizoTake/Virtual Light/Lit");
            Assert.That(shader, Is.Not.Null);
            var customEditorProperty = typeof(Shader).GetProperty("customEditor", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(customEditorProperty, Is.Not.Null);
            Assert.That(customEditorProperty.GetValue(shader), Is.EqualTo("MizoTake.VirtualLight.Editor.VirtualLightLitShaderGUI"));
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_ReceiveStandardLighting"), Is.True);
                Assert.That(material.GetFloat("_ReceiveStandardLighting"), Is.EqualTo(1f));
                material.SetFloat("_ReceiveStandardLighting", 0f);
                MaterialEditor.ApplyMaterialPropertyDrawers(material);
                Assert.That(material.IsKeywordEnabled("_RECEIVE_STANDARD_LIGHTING_OFF"), Is.True);
                material.SetFloat("_ReceiveStandardLighting", 1f);
                MaterialEditor.ApplyMaterialPropertyDrawers(material);
                Assert.That(material.IsKeywordEnabled("_RECEIVE_STANDARD_LIGHTING_OFF"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
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
        public void PackageSampleScene_DeclaresCurrentCoreFeatureMatrix()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VirtualLightDataTests).Assembly);
            Assert.That(packageInfo, Is.Not.Null);
            var scenePath = Path.Combine(packageInfo.resolvedPath, "Samples~", "Basic", "Scenes", "VirtualLightBasicSample.unity");
            var serializedScene = File.ReadAllText(scenePath);

            AssertSampleLight(serializedScene, "Directional Virtual Light", VirtualLightType.Directional, VirtualLightShape.Circle);
            AssertSampleLight(serializedScene, "Circle Point Virtual Light", VirtualLightType.Point, VirtualLightShape.Circle);
            AssertSampleLight(serializedScene, "Rectangle Point Virtual Light", VirtualLightType.Point, VirtualLightShape.Rectangle);
            AssertSampleLight(serializedScene, "Circle Spot Virtual Light", VirtualLightType.Spot, VirtualLightShape.Circle);
            AssertSampleLight(serializedScene, "Rectangle Spot Virtual Light", VirtualLightType.Spot, VirtualLightShape.Rectangle);
            AssertSampleLight(serializedScene, "Rectangle Area Virtual Light", VirtualLightType.RectangleArea, VirtualLightShape.Circle);
            StringAssert.Contains("areaSampleCount: 16", serializedScene);
            StringAssert.Contains("spotPenumbraSharpness: 1", serializedScene);
            StringAssert.Contains("orthographic size: 6.6", serializedScene);
        }

        [Test]
        public void PackageSampleReadme_MapsCoreAndAdvancedFeatureCoverage()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VirtualLightDataTests).Assembly);
            Assert.That(packageInfo, Is.Not.Null);
            var readme = File.ReadAllText(Path.Combine(packageInfo.resolvedPath, "Samples~", "Basic", "README.md"));

            foreach (var featureName in new[] { "Directional", "Circle Point", "Rectangle Point", "Circle Spot", "Rectangle Spot", "Rectangle Area", "custom shadows", "Gobo", "beam/impact", "performance" }) StringAssert.Contains(featureName, readme);
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

        private static void AssertSampleLight(string serializedScene, string objectName, VirtualLightType expectedType, VirtualLightShape expectedShape)
        {
            var blocks = serializedScene.Split(new[] { "--- !u!" }, StringSplitOptions.RemoveEmptyEntries);
            var gameObjectBlock = blocks.Single(block => block.StartsWith("1 &", StringComparison.Ordinal) && block.Contains($"m_Name: {objectName}"));
            var firstLine = gameObjectBlock.Substring(0, gameObjectBlock.IndexOf('\n'));
            var gameObjectId = firstLine.Substring(firstLine.IndexOf('&') + 1).Trim();
            var componentBlock = blocks.Single(block => block.StartsWith("114 &", StringComparison.Ordinal) && block.Contains($"m_GameObject: {{fileID: {gameObjectId}}}") && block.Contains("guid: 55b5d5760fafc6749acd7c03b801980b"));

            StringAssert.Contains($"type: {(int)expectedType}", componentBlock, $"{objectName} has the wrong Type.");
            StringAssert.Contains($"shape: {(int)expectedShape}", componentBlock, $"{objectName} has the wrong Shape.");
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
