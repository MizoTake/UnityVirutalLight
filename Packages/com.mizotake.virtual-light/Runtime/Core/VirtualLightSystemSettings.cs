using System;
using UnityEngine;

namespace MizoTake.VirtualLight
{
    public sealed class VirtualLightSystemSettings : ScriptableObject
    {
        public const string ResourcePath = "MizoTake/VirtualLight/VirtualLightSystemSettings";
        public const float DefaultShadowDepthBias = 0.0015f;
        public const float DefaultShadowNormalBias = 0.003f;
        public const int DefaultShadowCasterLayerMask = ~0;
        [SerializeField] private VirtualLightQuality quality = VirtualLightQuality.Medium;
        [SerializeField, Min(0f)] private float shadowDepthBias = DefaultShadowDepthBias;
        [SerializeField, Min(0f)] private float shadowNormalBias = DefaultShadowNormalBias;
        [SerializeField] private LayerMask shadowCasterLayers = DefaultShadowCasterLayerMask;

        public VirtualLightQuality Quality { get => SanitizeQuality(quality); set => quality = SanitizeQuality(value); }
        public float ShadowDepthBias { get => SanitizeBias(shadowDepthBias, DefaultShadowDepthBias); set => shadowDepthBias = SanitizeBias(value, DefaultShadowDepthBias); }
        public float ShadowNormalBias { get => SanitizeBias(shadowNormalBias, DefaultShadowNormalBias); set => shadowNormalBias = SanitizeBias(value, DefaultShadowNormalBias); }
        public LayerMask ShadowCasterLayers { get => shadowCasterLayers; set => shadowCasterLayers = value; }

        public static int GetShadowMapResolution(VirtualLightQuality value)
        {
            return SanitizeQuality(value) switch
            {
                VirtualLightQuality.Low => 256,
                VirtualLightQuality.Medium => 512,
                VirtualLightQuality.High => 768,
                VirtualLightQuality.Ultra => 1024,
                _ => 512
            };
        }

        public void ResetToDefaults()
        {
            quality = VirtualLightQuality.Medium;
            shadowDepthBias = DefaultShadowDepthBias;
            shadowNormalBias = DefaultShadowNormalBias;
            shadowCasterLayers = DefaultShadowCasterLayerMask;
        }

        internal void Normalize()
        {
            quality = SanitizeQuality(quality);
            shadowDepthBias = SanitizeBias(shadowDepthBias, DefaultShadowDepthBias);
            shadowNormalBias = SanitizeBias(shadowNormalBias, DefaultShadowNormalBias);
        }

        internal static VirtualLightQuality SanitizeQuality(VirtualLightQuality value)
        {
            return Enum.IsDefined(typeof(VirtualLightQuality), value) ? value : VirtualLightQuality.Medium;
        }

        internal static float SanitizeBias(float value, float fallback)
        {
            return float.IsFinite(value) ? Mathf.Max(0f, value) : fallback;
        }

        private void OnValidate()
        {
            Normalize();
        }
    }

    internal static class VirtualLightSystemSettingsLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyProjectSettings()
        {
            var settings = Resources.Load<VirtualLightSystemSettings>(VirtualLightSystemSettings.ResourcePath);
            if (settings != null) VirtualLightSystem.Current.ApplySettings(settings);
        }
    }
}
