using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MizoTake.VirtualLight
{
    internal static class VirtualLightGoboArray
    {
        private const int GoboResolution = 128;
        private static readonly int GoboParamsId = Shader.PropertyToID("_VirtualLightGoboParams");
        private static readonly int GoboTexturesId = Shader.PropertyToID("_VirtualLightGoboTextures");
        private static readonly Dictionary<Texture2D, int> SliceLookup = new Dictionary<Texture2D, int>();
        private static Vector4[] parameters = Array.Empty<Vector4>();
        private static Texture2D[] uploadedTextures = Array.Empty<Texture2D>();
        private static uint[] uploadedUpdateCounts = Array.Empty<uint>();
        private static GraphicsBuffer parameterBuffer;
        private static RenderTexture goboTextures;
        private static int parameterCapacity;
        private static int textureCapacity;

        internal static bool HasBindings => parameterBuffer != null;
        internal static bool HasTextureBinding => goboTextures != null && goboTextures.IsCreated();
        internal static int Resolution => GoboResolution;

        internal static void EnsureBindings()
        {
            BindUnmasked(1);
        }

        internal static void BindUnmasked(int lightCount)
        {
            var bindingCount = Mathf.Max(lightCount, 1);
            EnsureParameterCapacity(bindingCount);
            EnsureTextureCapacity(1);
            for (var index = 0; index < bindingCount; index++) parameters[index] = new Vector4(-1f, 0f, 0f, 0f);
            parameterBuffer.SetData(parameters, 0, 0, bindingCount);
            Shader.SetGlobalBuffer(GoboParamsId, parameterBuffer);
            Shader.SetGlobalTexture(GoboTexturesId, goboTextures);
        }

        internal static void Upload(VirtualLightDescriptor[] descriptors, int lightCount)
        {
            lightCount = Mathf.Clamp(lightCount, 0, descriptors?.Length ?? 0);
            EnsureParameterCapacity(Mathf.Max(lightCount, 1));
            SliceLookup.Clear();
            var textureCount = 0;
            for (var index = 0; index < lightCount; index++)
            {
                var texture = descriptors[index].GoboTexture;
                if (texture == null)
                {
                    parameters[index] = new Vector4(-1f, 0f, 0f, 0f);
                    continue;
                }
                if (!SliceLookup.TryGetValue(texture, out var slice))
                {
                    slice = textureCount++;
                    SliceLookup.Add(texture, slice);
                }
                parameters[index] = new Vector4(slice, 0f, 0f, 0f);
            }
            if (lightCount == 0) parameters[0] = new Vector4(-1f, 0f, 0f, 0f);
            EnsureTextureCapacity(Mathf.Max(textureCount, 1));
            foreach (var pair in SliceLookup)
            {
                var texture = pair.Key;
                var slice = pair.Value;
                if (uploadedTextures[slice] == texture && uploadedUpdateCounts[slice] == texture.updateCount) continue;
                Graphics.Blit(texture, goboTextures, 0, slice);
                uploadedTextures[slice] = texture;
                uploadedUpdateCounts[slice] = texture.updateCount;
            }
            parameterBuffer.SetData(parameters, 0, 0, Mathf.Max(lightCount, 1));
            Shader.SetGlobalBuffer(GoboParamsId, parameterBuffer);
            Shader.SetGlobalTexture(GoboTexturesId, goboTextures);
        }

        internal static void Dispose()
        {
            parameterBuffer?.Dispose();
            parameterBuffer = null;
            parameterCapacity = 0;
            parameters = Array.Empty<Vector4>();
            if (goboTextures != null)
            {
                goboTextures.Release();
                DestroyRuntimeObject(goboTextures);
            }
            goboTextures = null;
            textureCapacity = 0;
            uploadedTextures = Array.Empty<Texture2D>();
            uploadedUpdateCounts = Array.Empty<uint>();
            SliceLookup.Clear();
        }

        private static void EnsureParameterCapacity(int count)
        {
            if (parameterBuffer != null && parameterCapacity >= count) return;
            var replacementCapacity = GrowCapacity(parameterCapacity, count);
            var replacementParameters = new Vector4[replacementCapacity];
            var replacement = new GraphicsBuffer(GraphicsBuffer.Target.Structured, replacementCapacity, sizeof(float) * 4);
            parameterBuffer?.Dispose();
            parameterBuffer = replacement;
            parameterCapacity = replacementCapacity;
            parameters = replacementParameters;
        }

        private static void EnsureTextureCapacity(int count)
        {
            if (goboTextures != null && textureCapacity >= count && goboTextures.IsCreated()) return;
            var replacementCapacity = GrowCapacity(textureCapacity, count);
            var descriptor = new RenderTextureDescriptor(GoboResolution, GoboResolution, RenderTextureFormat.ARGB32, 0)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = replacementCapacity,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            var replacement = new RenderTexture(descriptor)
            {
                name = "Virtual Light Gobo Textures",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!replacement.Create())
            {
                DestroyRuntimeObject(replacement);
                throw new InvalidOperationException("Virtual Light gobo Texture2DArray allocation failed.");
            }
            Graphics.Blit(Texture2D.whiteTexture, replacement, 0, 0);
            var previous = goboTextures;
            goboTextures = replacement;
            textureCapacity = replacementCapacity;
            uploadedTextures = new Texture2D[replacementCapacity];
            uploadedUpdateCounts = new uint[replacementCapacity];
            if (previous != null)
            {
                previous.Release();
                DestroyRuntimeObject(previous);
            }
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

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
