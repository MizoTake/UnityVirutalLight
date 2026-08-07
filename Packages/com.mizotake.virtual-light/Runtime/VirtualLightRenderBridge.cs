using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MizoTake.VirtualLight
{
    internal static class VirtualLightRenderBridge
    {
        private const int TileSize = 16;
        private const int LightsPerTileMaskWord = sizeof(uint) * 8;
        private const int GpuStride = 80;
        private static readonly int LightCountId = Shader.PropertyToID("_VirtualLightCount");
        private static readonly int LightsId = Shader.PropertyToID("_VirtualLights");
        private static readonly int UseTilingId = Shader.PropertyToID("_VirtualLightUseTiling");
        private static readonly int TileCountsId = Shader.PropertyToID("_VirtualLightTileCounts");
        private static readonly int TileIndicesId = Shader.PropertyToID("_VirtualLightTileIndices");
        private static readonly int TileParamsId = Shader.PropertyToID("_VirtualLightTileParams");
        private static readonly int ViewProjectionId = Shader.PropertyToID("_VirtualLightViewProjection");
        private static readonly int ViewId = Shader.PropertyToID("_VirtualLightView");
        private static readonly int ProjectionScaleId = Shader.PropertyToID("_VirtualLightProjectionScale");
        private static readonly int ScreenSizeId = Shader.PropertyToID("_VirtualLightScreenSize");
        private static readonly int TileStrideId = Shader.PropertyToID("_VirtualLightTileStride");
        private static VirtualLightGpu[] selectedLights = Array.Empty<VirtualLightGpu>();
        private static VirtualLightDescriptor[] selectedDescriptors = Array.Empty<VirtualLightDescriptor>();
        private static VirtualLightHandle[] selectedHandles = Array.Empty<VirtualLightHandle>();
        private static GraphicsBuffer lightBuffer;
        private static GraphicsBuffer tileCountBuffer;
        private static GraphicsBuffer tileIndexBuffer;
        private static ComputeShader tileCullingShader;
        private static int kernel = -1;
        private static int lightCapacity;
        private static int tileCapacity;
        private static int tileIndexCapacity;
        private static bool initialized;
        internal static bool HasLightBinding => lightBuffer != null;
        internal static bool HasTileBindings => tileCountBuffer != null && tileIndexBuffer != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            VirtualLightSystem.ClearForRuntimeReset();
            Dispose();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeRuntime()
        {
            EnsureInitialized();
        }

        internal static void EnsureInitialized()
        {
            if (initialized) return;
            EnsureLightBuffer(1);
            EnsureTileBuffers(1, 1);
            Shader.SetGlobalBuffer(LightsId, lightBuffer);
            Shader.SetGlobalBuffer(TileCountsId, tileCountBuffer);
            Shader.SetGlobalBuffer(TileIndicesId, tileIndexBuffer);
            VirtualLightShadowMapArray.EnsureBindings();
            VirtualLightGoboArray.EnsureBindings();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Application.quitting += Dispose;
            initialized = true;
        }

        internal static void Dispose()
        {
            if (initialized)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                Application.quitting -= Dispose;
            }
            initialized = false;
            ReleaseGpuBuffers();
            VirtualLightShadowMapArray.Dispose();
            VirtualLightGoboArray.Dispose();
            tileCullingShader = null;
            kernel = -1;
            selectedLights = Array.Empty<VirtualLightGpu>();
            selectedDescriptors = Array.Empty<VirtualLightDescriptor>();
            selectedHandles = Array.Empty<VirtualLightHandle>();
            Shader.SetGlobalInt(LightCountId, 0);
            Shader.SetGlobalInt(UseTilingId, 0);
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null) return;
            EnsureSelectionCapacity(VirtualLightSystem.RegisteredCount);
            var count = VirtualLightSystem.FillSelected(camera.transform.position, selectedLights.Length, selectedLights, selectedDescriptors, selectedHandles);
            UploadGobos(count);
            VirtualLightShadowMapArray.Render(context, camera, selectedDescriptors, selectedHandles, selectedLights, count);
            Shader.SetGlobalInt(LightCountId, count);
            if (count <= 0)
            {
                Shader.SetGlobalInt(UseTilingId, 0);
                return;
            }
            if (TryUploadTiled(camera, count)) return;
            UploadStructured(count);
        }

        private static void UploadGobos(int count)
        {
            try
            {
                VirtualLightGoboArray.Upload(selectedDescriptors, count);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Virtual Light Gobo/Cookie textures are unavailable for this camera; lights remain unmasked. {exception.Message}");
                VirtualLightGoboArray.Dispose();
                VirtualLightGoboArray.BindUnmasked(count);
            }
        }

        private static bool TryUploadTiled(Camera camera, int count)
        {
            if (!SystemInfo.supportsComputeShaders) return false;
            tileCullingShader ??= Resources.Load<ComputeShader>("VirtualLightTileCulling");
            if (tileCullingShader == null) return false;
            if (kernel < 0) kernel = tileCullingShader.FindKernel("CullTiles");
            try
            {
                EnsureLightBuffer(count);
                lightBuffer.SetData(selectedLights, 0, 0, count);
                var width = Mathf.Max(camera.pixelWidth, 1);
                var height = Mathf.Max(camera.pixelHeight, 1);
                var tileCountX = Mathf.CeilToInt(width / (float)TileSize);
                var tileCountY = Mathf.CeilToInt(height / (float)TileSize);
                var tileCount = checked(tileCountX * tileCountY);
                var maskWordsPerTile = CalculateTileMaskWordCount(count);
                var tileMaskElementCount = CalculateTileMaskElementCount(tileCount, count);
                var tileIndexBytes = checked((long)tileMaskElementCount * sizeof(uint));
                var graphicsBufferLimit = SystemInfo.maxGraphicsBufferSize;
                var graphicsMemoryBudget = SystemInfo.graphicsMemorySize > 0 ? (long)SystemInfo.graphicsMemorySize * 1024L * 1024L / 8L : long.MaxValue;
                if ((graphicsBufferLimit > 0 && tileIndexBytes > graphicsBufferLimit) || tileIndexBytes > graphicsMemoryBudget) return false;
                EnsureTileBuffers(tileCount, tileMaskElementCount);
                var gpuProjection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, camera.targetTexture != null);
                tileCullingShader.SetInt(LightCountId, count);
                tileCullingShader.SetInt(TileStrideId, maskWordsPerTile);
                tileCullingShader.SetVector(ScreenSizeId, new Vector4(width, height, tileCountX, tileCountY));
                tileCullingShader.SetFloat(ProjectionScaleId, Mathf.Abs(gpuProjection.m11));
                tileCullingShader.SetMatrix(ViewId, camera.worldToCameraMatrix);
                tileCullingShader.SetMatrix(ViewProjectionId, gpuProjection * camera.worldToCameraMatrix);
                tileCullingShader.SetBuffer(kernel, LightsId, lightBuffer);
                tileCullingShader.SetBuffer(kernel, TileCountsId, tileCountBuffer);
                tileCullingShader.SetBuffer(kernel, TileIndicesId, tileIndexBuffer);
                tileCullingShader.Dispatch(kernel, tileCountX, tileCountY, 1);
                Shader.SetGlobalBuffer(LightsId, lightBuffer);
                Shader.SetGlobalBuffer(TileCountsId, tileCountBuffer);
                Shader.SetGlobalBuffer(TileIndicesId, tileIndexBuffer);
                Shader.SetGlobalVector(TileParamsId, new Vector4(tileCountX, tileCountY, TileSize, maskWordsPerTile));
                Shader.SetGlobalInt(UseTilingId, 1);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Virtual Light tiled culling is unavailable for this camera; all registered lights will use the dynamic linear buffer. {exception.Message}");
                ReleaseTileBuffers();
                return false;
            }
        }

        private static void UploadStructured(int count)
        {
            try
            {
                EnsureLightBuffer(count);
                lightBuffer.SetData(selectedLights, 0, 0, count);
                Shader.SetGlobalBuffer(LightsId, lightBuffer);
                Shader.SetGlobalInt(LightCountId, count);
                Shader.SetGlobalInt(UseTilingId, 2);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Virtual Light GPU buffer allocation failed; this camera cannot evaluate the registered lights. {exception.Message}");
                Shader.SetGlobalInt(LightCountId, 0);
                Shader.SetGlobalInt(UseTilingId, 0);
                ReleaseGpuBuffers();
            }
        }

        private static void EnsureSelectionCapacity(int count)
        {
            var currentCapacity = Math.Min(selectedLights.Length, Math.Min(selectedDescriptors.Length, selectedHandles.Length));
            if (currentCapacity >= count) return;
            var capacity = GrowCapacity(currentCapacity, count);
            var replacementLights = new VirtualLightGpu[capacity];
            var replacementDescriptors = new VirtualLightDescriptor[capacity];
            var replacementHandles = new VirtualLightHandle[capacity];
            selectedLights = replacementLights;
            selectedDescriptors = replacementDescriptors;
            selectedHandles = replacementHandles;
        }

        private static void EnsureLightBuffer(int count)
        {
            if (lightBuffer != null && lightCapacity >= count) return;
            var replacementCapacity = GrowCapacity(lightCapacity, count);
            var replacement = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementCapacity, GpuStride);
            var previous = lightBuffer;
            lightBuffer = replacement;
            lightCapacity = replacementCapacity;
            previous?.Dispose();
        }

        private static void EnsureTileBuffers(int tileCount, int indexCount)
        {
            var replaceTileCount = tileCountBuffer == null || tileCapacity < tileCount;
            var replaceTileIndices = tileIndexBuffer == null || tileIndexCapacity < indexCount;
            GraphicsBuffer replacementTileCountBuffer = null;
            GraphicsBuffer replacementTileIndexBuffer = null;
            var replacementTileCapacity = tileCapacity;
            var replacementTileIndexCapacity = tileIndexCapacity;
            try
            {
                if (replaceTileCount)
                {
                    replacementTileCapacity = GrowCapacity(tileCapacity, tileCount);
                    replacementTileCountBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementTileCapacity, sizeof(uint));
                }
                if (replaceTileIndices)
                {
                    replacementTileIndexCapacity = GrowCapacity(tileIndexCapacity, indexCount);
                    replacementTileIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementTileIndexCapacity, sizeof(uint));
                }
            }
            catch
            {
                replacementTileCountBuffer?.Dispose();
                replacementTileIndexBuffer?.Dispose();
                throw;
            }
            if (replaceTileCount)
            {
                tileCountBuffer?.Dispose();
                tileCountBuffer = replacementTileCountBuffer;
                tileCapacity = replacementTileCapacity;
            }
            if (replaceTileIndices)
            {
                tileIndexBuffer?.Dispose();
                tileIndexBuffer = replacementTileIndexBuffer;
                tileIndexCapacity = replacementTileIndexCapacity;
            }
        }

        private static int GrowCapacity(int current, int required)
        {
            if (required <= 1) return 1;
            var capacity = Math.Max(current, 1);
            while (capacity < required)
            {
                if (capacity > int.MaxValue / 2) return required;
                capacity *= 2;
            }
            return capacity;
        }

        internal static int CalculateTileMaskWordCount(int lightCount)
        {
            if (lightCount <= 0) return 0;
            return checked((int)(((long)lightCount + LightsPerTileMaskWord - 1L) / LightsPerTileMaskWord));
        }

        internal static int CalculateTileMaskElementCount(int tileCount, int lightCount)
        {
            if (tileCount <= 0 || lightCount <= 0) return 0;
            return checked(tileCount * CalculateTileMaskWordCount(lightCount));
        }

        private static void ReleaseTileBuffers()
        {
            tileCountBuffer?.Dispose();
            tileIndexBuffer?.Dispose();
            tileCountBuffer = null;
            tileIndexBuffer = null;
            tileCapacity = 0;
            tileIndexCapacity = 0;
        }

        private static void ReleaseGpuBuffers()
        {
            lightBuffer?.Dispose();
            lightBuffer = null;
            lightCapacity = 0;
            ReleaseTileBuffers();
        }
    }
}
