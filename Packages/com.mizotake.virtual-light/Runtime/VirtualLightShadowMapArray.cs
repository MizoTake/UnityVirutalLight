using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MizoTake.VirtualLight
{
    internal static class VirtualLightShadowMapArray
    {
        private static readonly int ShadowMapsId = Shader.PropertyToID("_VirtualLightShadowMaps");
        private static readonly int ShadowMatricesId = Shader.PropertyToID("_VirtualLightShadowMatrices");
        private static readonly int ShadowLightParamsId = Shader.PropertyToID("_VirtualLightShadowLightParams");
        private static readonly int ShadowCountId = Shader.PropertyToID("_VirtualLightShadowCount");
        private static readonly int ShadowSamplingParamsId = Shader.PropertyToID("_VirtualLightShadowSamplingParams");
        private static readonly int ShadowCasterPositionRangeId = Shader.PropertyToID("_VirtualLightShadowCasterPositionRange");
        private static Matrix4x4[] matrices = Array.Empty<Matrix4x4>();
        private static Vector4[] lightParams = Array.Empty<Vector4>();
        private static VirtualLightDescriptor[] shadowDescriptors = Array.Empty<VirtualLightDescriptor>();
        private static readonly List<Renderer> Renderers = new List<Renderer>();
        private static readonly List<Material> RendererMaterials = new List<Material>();
        private static readonly Dictionary<VirtualLightHandle, float> SourceApertures = new Dictionary<VirtualLightHandle, float>();
        private static readonly GraphicsFormat[] ShadowFormatCandidates = { GraphicsFormat.R32_SFloat, GraphicsFormat.R16_SFloat, GraphicsFormat.R16_UNorm };
        private static GraphicsBuffer matrixBuffer;
        private static GraphicsBuffer lightParamsBuffer;
        private static RenderTexture shadowMaps;
        private static Material shadowCasterMaterial;
        private static int capacity;
        private static int textureResolution;
        private static int textureSliceCount;
        private static GraphicsFormat textureFormat;
        internal static bool HasBindings => matrixBuffer != null && lightParamsBuffer != null;

        internal static void EnsureBindings()
        {
            if (matrixBuffer == null || lightParamsBuffer == null)
            {
                matrixBuffer?.Dispose();
                lightParamsBuffer?.Dispose();
                capacity = 1;
                matrices = new[] { Matrix4x4.identity };
                lightParams = new[] { Vector4.zero };
                shadowDescriptors = new VirtualLightDescriptor[capacity];
                matrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 16);
                lightParamsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 4);
                matrixBuffer.SetData(matrices);
                lightParamsBuffer.SetData(lightParams);
            }
            Shader.SetGlobalBuffer(ShadowMatricesId, matrixBuffer);
            Shader.SetGlobalBuffer(ShadowLightParamsId, lightParamsBuffer);
        }

        internal static void Render(ScriptableRenderContext context, Camera camera, VirtualLightDescriptor[] descriptors, VirtualLightHandle[] handles, VirtualLightGpu[] gpuLights, int lightCount)
        {
            EnsureBindings();
            for (var index = 0; index < lightCount; index++)
            {
                var cone = gpuLights[index].ConeShadowFlags;
                cone.z = -1f;
                gpuLights[index].ConeShadowFlags = cone;
            }
            var shadowCount = 0;
            for (var index = 0; index < lightCount; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor.Type != VirtualLightType.Spot || (descriptor.Flags & VirtualLightFlags.CastShadow) == 0) continue;
                shadowCount++;
            }
            if (shadowCount <= 0 || camera == null)
            {
                Shader.SetGlobalInt(ShadowCountId, 0);
                VirtualLightBeamVolume.ApplyShadowSlices(handles, gpuLights, lightCount);
                return;
            }
            try
            {
                var resolution = ShadowResolution(VirtualLightSystem.Quality);
                EnsureResources(shadowCount, resolution);
                VirtualLightBeamVolume.CollectSourceApertures(SourceApertures);
                var slot = 0;
                for (var index = 0; index < lightCount; index++)
                {
                    var descriptor = descriptors[index];
                    if (descriptor.Type != VirtualLightType.Spot || (descriptor.Flags & VirtualLightFlags.CastShadow) == 0) continue;
                    var shadowDescriptor = SourceApertures.TryGetValue(handles[index], out var sourceAperture) ? VirtualLightShadowMath.ExpandProjectionForSourceAperture(descriptor, sourceAperture) : descriptor;
                    shadowDescriptors[slot] = shadowDescriptor;
                    matrices[slot] = VirtualLightShadowMath.BuildViewProjection(shadowDescriptor);
                    lightParams[slot] = new Vector4(shadowDescriptor.Position.x, shadowDescriptor.Position.y, shadowDescriptor.Position.z, 1f / Mathf.Max(shadowDescriptor.Radius, 0.01f));
                    var cone = gpuLights[index].ConeShadowFlags;
                    cone.z = slot;
                    gpuLights[index].ConeShadowFlags = cone;
                    slot++;
                }
                matrixBuffer.SetData(matrices, 0, 0, shadowCount);
                lightParamsBuffer.SetData(lightParams, 0, 0, shadowCount);
                VirtualLightOccluder.CollectShadowRenderers(Renderers);
                RenderSlices(context, camera, shadowCount, resolution);
                VirtualLightBeamVolume.ApplyShadowSlices(handles, gpuLights, lightCount);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Virtual Light shadow maps are unavailable for this camera; lights remain unshadowed. {exception.Message}");
                Shader.SetGlobalInt(ShadowCountId, 0);
                for (var index = 0; index < lightCount; index++)
                {
                    var cone = gpuLights[index].ConeShadowFlags;
                    cone.z = -1f;
                    gpuLights[index].ConeShadowFlags = cone;
                }
                VirtualLightBeamVolume.ApplyShadowSlices(handles, gpuLights, lightCount);
            }
        }

        internal static void Dispose()
        {
            matrixBuffer?.Dispose();
            lightParamsBuffer?.Dispose();
            matrixBuffer = null;
            lightParamsBuffer = null;
            if (shadowMaps != null)
            {
                ReleaseRenderTexture(shadowMaps);
            }
            shadowMaps = null;
            if (shadowCasterMaterial != null) DestroyRuntimeObject(shadowCasterMaterial);
            shadowCasterMaterial = null;
            capacity = 0;
            textureResolution = 0;
            textureSliceCount = 0;
            textureFormat = GraphicsFormat.None;
            matrices = Array.Empty<Matrix4x4>();
            lightParams = Array.Empty<Vector4>();
            shadowDescriptors = Array.Empty<VirtualLightDescriptor>();
            Renderers.Clear();
            RendererMaterials.Clear();
            SourceApertures.Clear();
            Shader.SetGlobalInt(ShadowCountId, 0);
        }

        private static void EnsureResources(int shadowCount, int resolution)
        {
            if (capacity < shadowCount)
            {
                var replacementCapacity = GrowCapacity(capacity, shadowCount);
                var replacementMatrices = new Matrix4x4[replacementCapacity];
                var replacementLightParams = new Vector4[replacementCapacity];
                var replacementShadowDescriptors = new VirtualLightDescriptor[replacementCapacity];
                GraphicsBuffer replacementMatrixBuffer = null;
                GraphicsBuffer replacementLightParamsBuffer = null;
                try
                {
                    replacementMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementCapacity, sizeof(float) * 16);
                    replacementLightParamsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementCapacity, sizeof(float) * 4);
                }
                catch
                {
                    replacementMatrixBuffer?.Dispose();
                    replacementLightParamsBuffer?.Dispose();
                    throw;
                }
                matrixBuffer?.Dispose();
                lightParamsBuffer?.Dispose();
                capacity = replacementCapacity;
                matrices = replacementMatrices;
                lightParams = replacementLightParams;
                shadowDescriptors = replacementShadowDescriptors;
                matrixBuffer = replacementMatrixBuffer;
                lightParamsBuffer = replacementLightParamsBuffer;
                Shader.SetGlobalBuffer(ShadowMatricesId, matrixBuffer);
                Shader.SetGlobalBuffer(ShadowLightParamsId, lightParamsBuffer);
            }
            var format = ChooseShadowFormat();
            if (shadowMaps == null || textureResolution != resolution || textureSliceCount != shadowCount || textureFormat != format)
            {
                var descriptor = new RenderTextureDescriptor(resolution, resolution, format, 0)
                {
                    dimension = TextureDimension.Tex2DArray,
                    volumeDepth = shadowCount,
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    sRGB = false
                };
                var replacement = new RenderTexture(descriptor)
                {
                    name = "Virtual Light Shadow Maps",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!replacement.Create())
                {
                    DestroyRuntimeObject(replacement);
                    throw new InvalidOperationException("Texture2DArray allocation failed.");
                }
                var previous = shadowMaps;
                shadowMaps = replacement;
                textureResolution = resolution;
                textureSliceCount = shadowCount;
                textureFormat = format;
                if (previous != null) ReleaseRenderTexture(previous);
            }
            if (shadowCasterMaterial == null)
            {
                var shader = Resources.Load<Shader>("VirtualLightShadowCaster") ?? Shader.Find("Hidden/MizoTake/Virtual Light/Shadow Caster");
                if (shader == null) throw new InvalidOperationException("Virtual Light shadow caster shader was not found.");
                shadowCasterMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private static void RenderSlices(ScriptableRenderContext context, Camera camera, int shadowCount, int resolution)
        {
            var command = new CommandBuffer { name = "Virtual Light Shadow Maps" };
            try
            {
                var clearColor = Color.white;
                for (var slot = 0; slot < shadowCount; slot++)
                {
                    var descriptor = shadowDescriptors[slot];
                    command.SetRenderTarget(shadowMaps, 0, CubemapFace.Unknown, slot);
                    command.ClearRenderTarget(false, true, clearColor);
                    command.SetViewport(new Rect(0f, 0f, resolution, resolution));
                    command.SetViewProjectionMatrices(VirtualLightShadowMath.BuildView(descriptor), VirtualLightShadowMath.BuildProjection(descriptor));
                    command.SetGlobalVector(ShadowCasterPositionRangeId, new Vector4(descriptor.Position.x, descriptor.Position.y, descriptor.Position.z, 1f / Mathf.Max(descriptor.Radius, 0.01f)));
                    for (var rendererIndex = 0; rendererIndex < Renderers.Count; rendererIndex++)
                    {
                        var renderer = Renderers[rendererIndex];
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        RendererMaterials.Clear();
                        renderer.GetSharedMaterials(RendererMaterials);
                        var subMeshCount = GetSubMeshCount(renderer, RendererMaterials.Count);
                        for (var subMesh = 0; subMesh < subMeshCount; subMesh++)
                        {
                            if (!IsOpaqueSubMesh(RendererMaterials, subMesh)) continue;
                            command.DrawRenderer(renderer, shadowCasterMaterial, subMesh, 0);
                        }
                    }
                }
                if (camera.targetTexture != null) command.SetRenderTarget(camera.targetTexture);
                else command.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                command.SetViewport(new Rect(0f, 0f, Mathf.Max(camera.pixelWidth, 1), Mathf.Max(camera.pixelHeight, 1)));
                command.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                command.SetGlobalTexture(ShadowMapsId, shadowMaps);
                command.SetGlobalBuffer(ShadowMatricesId, matrixBuffer);
                command.SetGlobalBuffer(ShadowLightParamsId, lightParamsBuffer);
                command.SetGlobalInt(ShadowCountId, shadowCount);
                command.SetGlobalVector(ShadowSamplingParamsId, new Vector4(1f / resolution, 1f / resolution, 0.0015f, 0.003f));
                context.ExecuteCommandBuffer(command);
            }
            finally
            {
                command.Release();
            }
        }

        private static int ShadowResolution(VirtualLightQuality quality)
        {
            return quality switch
            {
                VirtualLightQuality.Low => 256,
                VirtualLightQuality.Medium => 512,
                VirtualLightQuality.High => 768,
                VirtualLightQuality.Ultra => 1024,
                _ => 512
            };
        }

        private static int GrowCapacity(int current, int required)
        {
            if (required <= 1) return 1;
            var result = Math.Max(current, 1);
            while (result < required)
            {
                if (result > int.MaxValue / 2) return required;
                result *= 2;
            }
            return result;
        }

        private static GraphicsFormat ChooseShadowFormat()
        {
            for (var index = 0; index < ShadowFormatCandidates.Length; index++)
            {
                var candidate = ShadowFormatCandidates[index];
                if (SystemInfo.IsFormatSupported(candidate, GraphicsFormatUsage.Render) && SystemInfo.IsFormatSupported(candidate, GraphicsFormatUsage.Blend) && SystemInfo.IsFormatSupported(candidate, GraphicsFormatUsage.Sample)) return candidate;
            }
            throw new NotSupportedException("No sampleable floating-point render target with minimum blending is available.");
        }

        private static int GetSubMeshCount(Renderer renderer, int materialCount)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null) return skinnedMeshRenderer.sharedMesh.subMeshCount;
            if (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>() is MeshFilter meshFilter && meshFilter.sharedMesh != null) return meshFilter.sharedMesh.subMeshCount;
            return materialCount;
        }

        private static bool IsOpaqueSubMesh(List<Material> materials, int subMesh)
        {
            if (materials.Count == 0) return false;
            var material = materials[Mathf.Min(subMesh, materials.Count - 1)];
            return material != null && material.renderQueue <= (int)RenderQueue.GeometryLast;
        }

        private static void ReleaseRenderTexture(RenderTexture texture)
        {
            texture.Release();
            DestroyRuntimeObject(texture);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
