using System.Collections.Generic;
using UnityEngine;

namespace MizoTake.VirtualLight
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class VirtualLightBeamVolume : MonoBehaviour
    {
        private static readonly int ShadowSliceId = Shader.PropertyToID("_VirtualLightShadowSlice");
        private static readonly int ScatteringIntensityId = Shader.PropertyToID("_ScatteringIntensity");
        private static readonly int SourceRadiusId = Shader.PropertyToID("_SourceRadius");
        private static readonly int GoboTextureId = Shader.PropertyToID("_VirtualLightGoboTexture");
        private static readonly int GoboEnabledId = Shader.PropertyToID("_VirtualLightGoboEnabled");
        private static readonly HashSet<VirtualLightBeamVolume> Active = new HashSet<VirtualLightBeamVolume>();
        [SerializeField, Min(0.0001f)] private float referenceIntensity;
        private VirtualLight virtualLight;
        private Renderer beamRenderer;
        private MaterialPropertyBlock propertyBlock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
        }

        private void OnEnable()
        {
            Active.Add(this);
            CacheComponents();
            if (referenceIntensity <= 0f && virtualLight != null) referenceIntensity = Mathf.Max(virtualLight.Intensity, 0.0001f);
            SetRenderingProperties(-1f);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void OnValidate()
        {
            referenceIntensity = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(referenceIntensity));
            CacheComponents();
            if (referenceIntensity <= 0f && virtualLight != null) referenceIntensity = Mathf.Max(virtualLight.Intensity, 0.0001f);
            SetRenderingProperties(-1f);
        }

        internal static void ApplyShadowSlices(VirtualLightHandle[] handles, VirtualLightGpu[] lights, int lightCount)
        {
            foreach (var volume in Active)
            {
                if (volume == null) continue;
                volume.CacheComponents();
                var slice = -1f;
                if (volume.virtualLight != null)
                {
                    var handle = volume.virtualLight.Handle;
                    for (var index = 0; index < lightCount; index++)
                    {
                        if (handles[index] != handle) continue;
                        slice = lights[index].ConeShadowFlags.z;
                        break;
                    }
                }
                volume.SetRenderingProperties(slice);
            }
        }

        internal static void CollectSourceApertures(Dictionary<VirtualLightHandle, float> apertures)
        {
            apertures.Clear();
            foreach (var volume in Active)
            {
                if (volume == null) continue;
                volume.CacheComponents();
                if (volume.virtualLight == null || volume.beamRenderer == null) continue;
                var handle = volume.virtualLight.Handle;
                var material = volume.beamRenderer.sharedMaterial;
                if (!handle.IsValid || material == null || !material.HasProperty(SourceRadiusId)) continue;
                var sourceAperture = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(material.GetFloat(SourceRadiusId)));
                if (sourceAperture <= 0f) continue;
                if (!apertures.TryGetValue(handle, out var previous) || sourceAperture > previous) apertures[handle] = sourceAperture;
            }
        }

        internal float GetSourceAperture()
        {
            CacheComponents();
            var material = beamRenderer != null ? beamRenderer.sharedMaterial : null;
            return material != null && material.HasProperty(SourceRadiusId) ? Mathf.Max(0f, VirtualLightMath.FiniteOrZero(material.GetFloat(SourceRadiusId))) : 0f;
        }

        private void CacheComponents()
        {
            virtualLight ??= GetComponentInParent<VirtualLight>();
            beamRenderer ??= GetComponent<Renderer>();
            propertyBlock ??= new MaterialPropertyBlock();
        }

        private void SetRenderingProperties(float slice)
        {
            if (beamRenderer == null) return;
            beamRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ShadowSliceId, slice);
            var intensityScale = virtualLight != null ? virtualLight.Intensity / Mathf.Max(referenceIntensity, 0.0001f) : 0f;
            var material = beamRenderer.sharedMaterial;
            if (material != null && material.HasProperty(ScatteringIntensityId)) propertyBlock.SetFloat(ScatteringIntensityId, material.GetFloat(ScatteringIntensityId) * Mathf.Max(0f, VirtualLightMath.FiniteOrZero(intensityScale)));
            var goboTexture = virtualLight != null ? virtualLight.GoboTexture : null;
            propertyBlock.SetTexture(GoboTextureId, goboTexture != null ? goboTexture : Texture2D.whiteTexture);
            propertyBlock.SetFloat(GoboEnabledId, goboTexture != null ? 1f : 0f);
            beamRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
