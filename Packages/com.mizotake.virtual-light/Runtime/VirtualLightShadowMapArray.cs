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
        private static readonly int ShadowDirectionsId = Shader.PropertyToID("_VirtualLightShadowDirections");
        private static readonly int ShadowCountId = Shader.PropertyToID("_VirtualLightShadowCount");
        private static readonly int ShadowSamplingParamsId = Shader.PropertyToID("_VirtualLightShadowSamplingParams");
        private static readonly int ShadowCasterPositionRangeId = Shader.PropertyToID("_VirtualLightShadowCasterPositionRange");
        private static readonly int ShadowCasterDirectionModeId = Shader.PropertyToID("_VirtualLightShadowCasterDirectionMode");
        private static Matrix4x4[] matrices = Array.Empty<Matrix4x4>();
        private static Vector4[] lightParams = Array.Empty<Vector4>();
        private static Vector4[] directions = Array.Empty<Vector4>();
        private static ShadowSliceData[] shadowSlices = Array.Empty<ShadowSliceData>();
        private static readonly List<Renderer> Renderers = new List<Renderer>();
        private static readonly List<Material> RendererMaterials = new List<Material>();
        private static readonly Dictionary<VirtualLightHandle, float> SourceApertures = new Dictionary<VirtualLightHandle, float>();
        private static readonly GraphicsFormat[] ShadowFormatCandidates = { GraphicsFormat.R32_SFloat, GraphicsFormat.R16_SFloat, GraphicsFormat.R16_UNorm };
        private static GraphicsBuffer matrixBuffer;
        private static GraphicsBuffer lightParamsBuffer;
        private static GraphicsBuffer directionBuffer;
        private static RenderTexture shadowMaps;
        private static Material shadowCasterMaterial;
        private static int capacity;
        private static int textureResolution;
        private static int textureSliceCount;
        private static GraphicsFormat textureFormat;
        internal static bool HasBindings => matrixBuffer != null && lightParamsBuffer != null && directionBuffer != null;

        private struct ShadowSliceData
        {
            public Matrix4x4 View;
            public Matrix4x4 Projection;
            public Vector4 PositionRange;
            public Vector4 DirectionMode;
        }

        internal static void EnsureBindings()
        {
            if (matrixBuffer == null || lightParamsBuffer == null || directionBuffer == null)
            {
                matrixBuffer?.Dispose();
                lightParamsBuffer?.Dispose();
                directionBuffer?.Dispose();
                capacity = 1;
                matrices = new[] { Matrix4x4.identity };
                lightParams = new[] { Vector4.zero };
                directions = new[] { new Vector4(0f, 0f, 1f, 0f) };
                shadowSlices = new ShadowSliceData[capacity];
                GraphicsBuffer replacementMatrixBuffer = null;
                GraphicsBuffer replacementLightParamsBuffer = null;
                GraphicsBuffer replacementDirectionBuffer = null;
                try
                {
                    replacementMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 16);
                    replacementLightParamsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 4);
                    replacementDirectionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 4);
                    replacementMatrixBuffer.SetData(matrices);
                    replacementLightParamsBuffer.SetData(lightParams);
                    replacementDirectionBuffer.SetData(directions);
                }
                catch
                {
                    replacementMatrixBuffer?.Dispose();
                    replacementLightParamsBuffer?.Dispose();
                    replacementDirectionBuffer?.Dispose();
                    throw;
                }
                matrixBuffer = replacementMatrixBuffer;
                lightParamsBuffer = replacementLightParamsBuffer;
                directionBuffer = replacementDirectionBuffer;
            }
            Shader.SetGlobalBuffer(ShadowMatricesId, matrixBuffer);
            Shader.SetGlobalBuffer(ShadowLightParamsId, lightParamsBuffer);
            Shader.SetGlobalBuffer(ShadowDirectionsId, directionBuffer);
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
                if ((descriptor.Flags & VirtualLightFlags.CastShadow) == 0) continue;
                shadowCount = checked(shadowCount + VirtualLightShadowMath.GetSliceCount(descriptor));
            }
            if (shadowCount <= 0 || camera == null)
            {
                Shader.SetGlobalInt(ShadowCountId, 0);
                VirtualLightBeamVolume.ApplyShadowSlices(handles, gpuLights, lightCount);
                return;
            }
            try
            {
                var resolution = VirtualLightSystemSettings.GetShadowMapResolution(VirtualLightSystem.Quality);
                EnsureResources(shadowCount, resolution);
                VirtualLightBeamVolume.CollectSourceApertures(SourceApertures);
                VirtualLightOccluder.CollectShadowRenderers(Renderers);
                var slot = 0;
                for (var index = 0; index < lightCount; index++)
                {
                    var descriptor = descriptors[index];
                    if ((descriptor.Flags & VirtualLightFlags.CastShadow) == 0 || VirtualLightShadowMath.GetSliceCount(descriptor) == 0) continue;
                    if (descriptor.Type == VirtualLightType.Spot && SourceApertures.TryGetValue(handles[index], out var sourceAperture)) descriptor = VirtualLightShadowMath.ExpandProjectionForSourceAperture(descriptor, sourceAperture);
                    var cone = gpuLights[index].ConeShadowFlags;
                    cone.z = slot;
                    gpuLights[index].ConeShadowFlags = cone;
                    slot += PopulateSlices(descriptor, camera, slot);
                }
                matrixBuffer.SetData(matrices, 0, 0, shadowCount);
                lightParamsBuffer.SetData(lightParams, 0, 0, shadowCount);
                directionBuffer.SetData(directions, 0, 0, shadowCount);
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

        private static int PopulateSlices(in VirtualLightDescriptor source, Camera camera, int firstSlice)
        {
            var descriptor = source.Sanitized();
            var inverseRange = 1f / Mathf.Max(descriptor.Radius, 0.01f);
            var positionRange = new Vector4(descriptor.Position.x, descriptor.Position.y, descriptor.Position.z, inverseRange);
            switch (descriptor.Type)
            {
                case VirtualLightType.Point:
                {
                    var projection = VirtualLightShadowMath.BuildPointProjection(descriptor);
                    for (var faceIndex = 0; faceIndex < 6; faceIndex++)
                    {
                        var face = (CubemapFace)faceIndex;
                        var direction = VirtualLightShadowMath.GetPointFaceDirection(face);
                        WriteSlice(firstSlice + faceIndex, VirtualLightShadowMath.BuildPointView(descriptor, face), projection, positionRange, new Vector4(direction.x, direction.y, direction.z, 0f));
                    }
                    return 6;
                }
                case VirtualLightType.Spot:
                {
                    var direction = VirtualLightMath.NormalizeOrForward(descriptor.Direction);
                    WriteSlice(firstSlice, VirtualLightShadowMath.BuildView(descriptor), VirtualLightShadowMath.BuildProjection(descriptor), positionRange, new Vector4(direction.x, direction.y, direction.z, 0f));
                    return 1;
                }
                case VirtualLightType.RectangleArea:
                {
                    var direction = VirtualLightMath.NormalizeOrForward(descriptor.Direction);
                    var projection = VirtualLightShadowMath.BuildAreaProjection(descriptor);
                    WriteSlice(firstSlice, VirtualLightShadowMath.BuildAreaView(descriptor, false), projection, positionRange, new Vector4(direction.x, direction.y, direction.z, 1f));
                    WriteSlice(firstSlice + 1, VirtualLightShadowMath.BuildAreaView(descriptor, true), projection, positionRange, new Vector4(-direction.x, -direction.y, -direction.z, 1f));
                    return 2;
                }
                case VirtualLightType.Directional:
                {
                    var direction = VirtualLightMath.NormalizeOrForward(descriptor.Direction);
                    var view = VirtualLightShadowMath.BuildDirectionalView(descriptor, camera, out var depthOrigin);
                    positionRange = new Vector4(depthOrigin.x, depthOrigin.y, depthOrigin.z, inverseRange);
                    WriteSlice(firstSlice, view, VirtualLightShadowMath.BuildDirectionalProjection(descriptor), positionRange, new Vector4(direction.x, direction.y, direction.z, 1f));
                    return 1;
                }
                default:
                    return 0;
            }
        }

        private static void WriteSlice(int slice, Matrix4x4 view, Matrix4x4 projection, Vector4 positionRange, Vector4 directionMode)
        {
            matrices[slice] = GL.GetGPUProjectionMatrix(projection, false) * view;
            lightParams[slice] = positionRange;
            directions[slice] = directionMode;
            shadowSlices[slice] = new ShadowSliceData { View = view, Projection = projection, PositionRange = positionRange, DirectionMode = directionMode };
        }

        internal static void Dispose()
        {
            matrixBuffer?.Dispose();
            lightParamsBuffer?.Dispose();
            directionBuffer?.Dispose();
            matrixBuffer = null;
            lightParamsBuffer = null;
            directionBuffer = null;
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
            directions = Array.Empty<Vector4>();
            shadowSlices = Array.Empty<ShadowSliceData>();
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
                var replacementDirections = new Vector4[replacementCapacity];
                var replacementShadowSlices = new ShadowSliceData[replacementCapacity];
                GraphicsBuffer replacementMatrixBuffer = null;
                GraphicsBuffer replacementLightParamsBuffer = null;
                GraphicsBuffer replacementDirectionBuffer = null;
                try
                {
                    replacementMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementCapacity, sizeof(float) * 16);
                    replacementLightParamsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementCapacity, sizeof(float) * 4);
                    replacementDirectionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementCapacity, sizeof(float) * 4);
                }
                catch
                {
                    replacementMatrixBuffer?.Dispose();
                    replacementLightParamsBuffer?.Dispose();
                    replacementDirectionBuffer?.Dispose();
                    throw;
                }
                matrixBuffer?.Dispose();
                lightParamsBuffer?.Dispose();
                directionBuffer?.Dispose();
                capacity = replacementCapacity;
                matrices = replacementMatrices;
                lightParams = replacementLightParams;
                directions = replacementDirections;
                shadowSlices = replacementShadowSlices;
                matrixBuffer = replacementMatrixBuffer;
                lightParamsBuffer = replacementLightParamsBuffer;
                directionBuffer = replacementDirectionBuffer;
                Shader.SetGlobalBuffer(ShadowMatricesId, matrixBuffer);
                Shader.SetGlobalBuffer(ShadowLightParamsId, lightParamsBuffer);
                Shader.SetGlobalBuffer(ShadowDirectionsId, directionBuffer);
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
                    var slice = shadowSlices[slot];
                    command.SetRenderTarget(shadowMaps, 0, CubemapFace.Unknown, slot);
                    command.ClearRenderTarget(false, true, clearColor);
                    command.SetViewport(new Rect(0f, 0f, resolution, resolution));
                    command.SetViewProjectionMatrices(slice.View, slice.Projection);
                    command.SetGlobalVector(ShadowCasterPositionRangeId, slice.PositionRange);
                    command.SetGlobalVector(ShadowCasterDirectionModeId, slice.DirectionMode);
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
                command.SetGlobalBuffer(ShadowDirectionsId, directionBuffer);
                command.SetGlobalInt(ShadowCountId, shadowCount);
                command.SetGlobalVector(ShadowSamplingParamsId, BuildShadowSamplingParameters(resolution));
                context.ExecuteCommandBuffer(command);
            }
            finally
            {
                command.Release();
            }
        }

        internal static Vector4 BuildShadowSamplingParameters(int resolution)
        {
            var inverseResolution = 1f / Mathf.Max(resolution, 1);
            return new Vector4(inverseResolution, inverseResolution, VirtualLightSystem.ShadowDepthBias, VirtualLightSystem.ShadowNormalBias);
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
