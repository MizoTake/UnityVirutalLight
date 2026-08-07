using Unity.Profiling;
using UnityEngine;

namespace MizoTake.VirtualLight
{
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VirtualLight))]
    [AddComponentMenu("Rendering/Virtual Light Beam Occlusion")]
    public sealed class VirtualLightBeamOcclusion : MonoBehaviour
    {
        private const int HitCapacity = 32;
        private const int MaximumHitCapacity = 256;
        private static readonly ProfilerMarker RefreshMarker = new ProfilerMarker("VirtualLight.BeamOcclusion.Refresh");
        private static readonly ProfilerMarker PhysicsQueryMarker = new ProfilerMarker("VirtualLight.BeamOcclusion.PhysicsQuery");
        private static readonly ProfilerMarker VisualUpdateMarker = new ProfilerMarker("VirtualLight.BeamOcclusion.UpdateVisuals");
        private static readonly int ImpactInnerRatioId = Shader.PropertyToID("_InnerRatio");
        private static readonly int GoboTextureId = Shader.PropertyToID("_VirtualLightGoboTexture");
        private static readonly int GoboEnabledId = Shader.PropertyToID("_VirtualLightGoboEnabled");
        [SerializeField] private Transform beamVisual;
        [SerializeField] private Transform impactVisual;
        [SerializeField] private LayerMask occluderLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private bool requireOccluderMarker = true;
        [SerializeField] private bool fitVisualToSpotCone = true;
        [SerializeField] private bool fitImpactToSpotCone = true;
        [SerializeField] private bool truncateVisualAtFirstHit;
        [SerializeField, Min(0f)] private float probeRadius;
        [SerializeField, Min(0f)] private float surfaceOffset = 0.01f;
        [SerializeField, Min(1f)] private float maximumImpactAspectRatio = 8f;
        [SerializeField, Min(0f)] private float maximumRefreshRate = 60f;
        private RaycastHit[] hits = new RaycastHit[HitCapacity];
        private RaycastHit[] impactHits = new RaycastHit[HitCapacity];
        private VirtualLight virtualLight;
        private VirtualLightBeamVolume beamVolume;
        private Renderer impactRenderer;
        private MaterialPropertyBlock impactPropertyBlock;
        private bool impactRendererResolved;
        private RaycastHit lastImpactHit;
        private float appliedImpactInnerRatio = -1f;
        private Texture2D appliedImpactGoboTexture;
        private double nextAutomaticRefreshTime;

        public Transform BeamVisual { get => beamVisual; set { beamVisual = value; EnsureBeamVolumeMarker(); UpdateVisuals(CurrentVisibleDistance, IsBlocked, lastImpactHit); } }
        public Transform ImpactVisual { get => impactVisual; set { impactVisual = value; impactRenderer = null; impactRendererResolved = false; appliedImpactInnerRatio = -1f; appliedImpactGoboTexture = null; UpdateVisuals(CurrentVisibleDistance, IsBlocked, lastImpactHit); } }
        public LayerMask OccluderLayers { get => occluderLayers; set => occluderLayers = value; }
        public bool RequireOccluderMarker { get => requireOccluderMarker; set => requireOccluderMarker = value; }
        public bool FitVisualToSpotCone { get => fitVisualToSpotCone; set { fitVisualToSpotCone = value; UpdateVisuals(CurrentVisibleDistance, IsBlocked, lastImpactHit); } }
        public bool FitImpactToSpotCone { get => fitImpactToSpotCone; set { fitImpactToSpotCone = value; UpdateVisuals(CurrentVisibleDistance, IsBlocked, lastImpactHit); } }
        public bool TruncateVisualAtFirstHit { get => truncateVisualAtFirstHit; set { truncateVisualAtFirstHit = value; UpdateVisuals(CurrentVisibleDistance, IsBlocked, lastImpactHit); } }
        public float ProbeRadius { get => probeRadius; set => probeRadius = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(value)); }
        public float SurfaceOffset { get => surfaceOffset; set => surfaceOffset = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(value)); }
        public float MaximumImpactAspectRatio { get => maximumImpactAspectRatio; set => maximumImpactAspectRatio = Mathf.Max(1f, VirtualLightMath.FiniteOrZero(value)); }
        public float MaximumRefreshRate { get => maximumRefreshRate; set => maximumRefreshRate = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(value)); }
        public float CurrentVisibleDistance { get; private set; }
        public bool IsBlocked { get; private set; }
        public VirtualLightBeamFootprint CurrentImpactFootprint { get; private set; }
        public int HitBufferCapacity => Mathf.Max(hits.Length, impactHits.Length);
        public bool HitBufferSaturated { get; private set; }

        private void OnEnable()
        {
            virtualLight = GetComponent<VirtualLight>();
            impactRenderer = null;
            impactRendererResolved = false;
            appliedImpactInnerRatio = -1f;
            appliedImpactGoboTexture = null;
            NormalizeValues();
            EnsureBeamVolumeMarker();
            if (beamVisual != null) beamVisual.gameObject.SetActive(true);
            RefreshNow();
            ScheduleNextAutomaticRefresh();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && maximumRefreshRate > 0f)
            {
                var now = Time.unscaledTimeAsDouble;
                if (now < nextAutomaticRefreshTime) return;
            }
            RefreshNow();
            ScheduleNextAutomaticRefresh();
        }

        private void OnDisable()
        {
            if (virtualLight != null) virtualLight.OcclusionDistance = -1f;
            if (beamVisual != null) beamVisual.gameObject.SetActive(false);
            if (impactVisual != null) impactVisual.gameObject.SetActive(false);
            CurrentVisibleDistance = 0f;
            IsBlocked = false;
            CurrentImpactFootprint = default;
            lastImpactHit = default;
        }

        private void OnValidate()
        {
            NormalizeValues();
            if (isActiveAndEnabled) RefreshNow();
        }

        public void RefreshNow()
        {
            using (RefreshMarker.Auto())
            {
                HitBufferSaturated = false;
                virtualLight ??= GetComponent<VirtualLight>();
                if (virtualLight == null || virtualLight.Type != VirtualLightType.Spot)
                {
                    if (virtualLight != null && virtualLight.OcclusionDistance >= 0f) virtualLight.OcclusionDistance = -1f;
                    CurrentVisibleDistance = virtualLight != null ? virtualLight.Range : 0f;
                    IsBlocked = false;
                    lastImpactHit = default;
                    UpdateVisuals(CurrentVisibleDistance, false, default);
                    return;
                }
                var maximumDistance = virtualLight.Range;
                var hasHit = TryFindNearestOccluder(maximumDistance, out var nearestHit, out var nearestDistance);
                CurrentVisibleDistance = VirtualLightMath.ResolveVisibleDistance(maximumDistance, hasHit ? nearestDistance : float.PositiveInfinity, surfaceOffset);
                IsBlocked = hasHit && CurrentVisibleDistance < maximumDistance;
                lastImpactHit = hasHit ? nearestHit : default;
                if (hasHit && fitImpactToSpotCone && probeRadius > 0.0001f && !TryFindNearestCenterRayOccluder(maximumDistance, out lastImpactHit)) lastImpactHit = default;
                if (!Mathf.Approximately(virtualLight.OcclusionDistance, CurrentVisibleDistance)) virtualLight.OcclusionDistance = CurrentVisibleDistance;
                UpdateVisuals(CurrentVisibleDistance, IsBlocked, lastImpactHit);
            }
        }

        private bool TryFindNearestOccluder(float maximumDistance, out RaycastHit nearestHit, out float nearestDistance)
        {
            var origin = transform.position;
            var direction = VirtualLightMath.NormalizeOrForward(transform.forward);
            var hitCount = QueryNonAlloc(ref hits, origin, direction, maximumDistance, probeRadius > 0.0001f);
            nearestDistance = float.PositiveInfinity;
            nearestHit = default;
            for (var index = 0; index < hitCount; index++)
            {
                var candidate = hits[index];
                if (!IsAcceptedCandidate(candidate)) continue;
                var projectedDistance = VirtualLightMath.ResolveProjectedHitDistance(origin, direction, candidate.point, candidate.distance, maximumDistance);
                if (projectedDistance >= nearestDistance) continue;
                nearestDistance = projectedDistance;
                nearestHit = candidate;
            }
            return float.IsFinite(nearestDistance);
        }

        private bool TryFindNearestCenterRayOccluder(float maximumDistance, out RaycastHit nearestHit)
        {
            var origin = transform.position;
            var direction = VirtualLightMath.NormalizeOrForward(transform.forward);
            var hitCount = QueryNonAlloc(ref impactHits, origin, direction, maximumDistance, false);
            var nearestDistance = float.PositiveInfinity;
            nearestHit = default;
            for (var index = 0; index < hitCount; index++)
            {
                var candidate = impactHits[index];
                if (!IsAcceptedCandidate(candidate)) continue;
                var projectedDistance = VirtualLightMath.ResolveProjectedHitDistance(origin, direction, candidate.point, candidate.distance, maximumDistance);
                if (projectedDistance >= nearestDistance) continue;
                nearestDistance = projectedDistance;
                nearestHit = candidate;
            }
            return float.IsFinite(nearestDistance);
        }

        private int QueryNonAlloc(ref RaycastHit[] buffer, Vector3 origin, Vector3 direction, float maximumDistance, bool sphereCast)
        {
            while (true)
            {
                int hitCount;
                using (PhysicsQueryMarker.Auto()) hitCount = sphereCast ? Physics.SphereCastNonAlloc(origin, probeRadius, direction, buffer, maximumDistance, occluderLayers, triggerInteraction) : Physics.RaycastNonAlloc(origin, direction, buffer, maximumDistance, occluderLayers, triggerInteraction);
                if (hitCount < buffer.Length) return hitCount;
                if (buffer.Length >= MaximumHitCapacity)
                {
                    HitBufferSaturated = true;
                    return hitCount;
                }
                System.Array.Resize(ref buffer, Mathf.Min(buffer.Length * 2, MaximumHitCapacity));
            }
        }

        private bool IsAcceptedCandidate(RaycastHit candidate)
        {
            if (candidate.collider == null || candidate.collider.transform.IsChildOf(transform) || candidate.collider.GetComponentInParent<VirtualLightBeamVolume>() != null) return false;
            var marker = candidate.collider.GetComponentInParent<VirtualLightOccluder>();
            if (requireOccluderMarker && (marker == null || !marker.isActiveAndEnabled || !marker.BlocksBeam)) return false;
            return marker == null || marker.isActiveAndEnabled && marker.BlocksBeam;
        }

        private void UpdateVisuals(float visibleDistance, bool blocked, RaycastHit hit)
        {
            using (VisualUpdateMarker.Auto())
            {
                if (beamVisual != null)
                {
                    var visualDistance = truncateVisualAtFirstHit ? visibleDistance : virtualLight != null ? virtualLight.Range : visibleDistance;
                    SetWorldPoseIfChanged(beamVisual, transform.position + transform.forward * visualDistance * 0.5f, transform.rotation);
                    if (fitVisualToSpotCone)
                    {
                        var radius = VirtualLightMath.EvaluateBeamRadius(visualDistance, virtualLight != null ? virtualLight.OuterAngle : 0f);
                        SetLocalScaleIfChanged(beamVisual, new Vector3(radius * 2f, radius * 2f, visualDistance));
                    }
                    else
                    {
                        var scale = beamVisual.localScale;
                        SetLocalScaleIfChanged(beamVisual, new Vector3(scale.x, scale.y, visualDistance));
                    }
                }
                if (impactVisual == null)
                {
                    CurrentImpactFootprint = default;
                    return;
                }
                CurrentImpactFootprint = default;
                if (!blocked || virtualLight == null || !VirtualLightMath.TryEvaluateBeamFootprint(transform.position, transform.forward, transform.right, hit.point, hit.normal, virtualLight.Range, virtualLight.OuterAngle, ResolveSourceAperture(), out var footprint) || footprint.AspectRatio > maximumImpactAspectRatio)
                {
                    SetActiveIfChanged(impactVisual.gameObject, false);
                    return;
                }
                CurrentImpactFootprint = footprint;
                SetActiveIfChanged(impactVisual.gameObject, true);
                SetWorldPoseIfChanged(impactVisual, footprint.Center + footprint.SurfaceNormal * surfaceOffset, footprint.Rotation);
                UpdateImpactMaterialProperties();
                if (fitImpactToSpotCone)
                {
                    var scale = impactVisual.localScale;
                    SetLocalScaleIfChanged(impactVisual, new Vector3(footprint.Diameter.x, footprint.Diameter.y, scale.z));
                }
            }
        }

        private void NormalizeValues()
        {
            probeRadius = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(probeRadius));
            surfaceOffset = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(surfaceOffset));
            maximumImpactAspectRatio = Mathf.Max(1f, VirtualLightMath.FiniteOrZero(maximumImpactAspectRatio));
            maximumRefreshRate = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(maximumRefreshRate));
        }

        private void EnsureBeamVolumeMarker()
        {
            if (beamVisual == null)
            {
                beamVolume = null;
                return;
            }
            beamVolume = beamVisual.GetComponent<VirtualLightBeamVolume>();
            if (beamVolume == null) beamVolume = beamVisual.gameObject.AddComponent<VirtualLightBeamVolume>();
        }

        private float ResolveSourceAperture()
        {
            if (beamVisual == null) return 0f;
            beamVolume ??= beamVisual.GetComponent<VirtualLightBeamVolume>();
            return beamVolume != null ? beamVolume.GetSourceAperture() : 0f;
        }

        private void UpdateImpactMaterialProperties()
        {
            if (!impactRendererResolved)
            {
                impactRenderer = impactVisual != null ? impactVisual.GetComponent<Renderer>() : null;
                impactRendererResolved = true;
            }
            var material = impactRenderer != null ? impactRenderer.sharedMaterial : null;
            if (material == null || virtualLight == null) return;
            var outerRadius = VirtualLightMath.EvaluateBeamRadius(1f, virtualLight.OuterAngle);
            var innerRatio = outerRadius > 0.000001f ? Mathf.Clamp01(VirtualLightMath.EvaluateBeamRadius(1f, virtualLight.InnerAngle) / outerRadius) : 0f;
            var goboTexture = virtualLight.GoboTexture;
            if (Mathf.Abs(appliedImpactInnerRatio - innerRatio) <= 0.0001f && appliedImpactGoboTexture == goboTexture) return;
            impactPropertyBlock ??= new MaterialPropertyBlock();
            impactRenderer.GetPropertyBlock(impactPropertyBlock);
            if (material.HasProperty(ImpactInnerRatioId)) impactPropertyBlock.SetFloat(ImpactInnerRatioId, innerRatio);
            impactPropertyBlock.SetTexture(GoboTextureId, goboTexture != null ? goboTexture : Texture2D.whiteTexture);
            impactPropertyBlock.SetFloat(GoboEnabledId, goboTexture != null ? 1f : 0f);
            impactRenderer.SetPropertyBlock(impactPropertyBlock);
            appliedImpactInnerRatio = innerRatio;
            appliedImpactGoboTexture = goboTexture;
        }

        private void ScheduleNextAutomaticRefresh()
        {
            nextAutomaticRefreshTime = maximumRefreshRate > 0f ? Time.unscaledTimeAsDouble + 1.0 / maximumRefreshRate : 0.0;
        }

        private static void SetActiveIfChanged(GameObject target, bool active)
        {
            if (target.activeSelf != active) target.SetActive(active);
        }

        private static void SetWorldPoseIfChanged(Transform target, Vector3 position, Quaternion rotation)
        {
            if ((target.position - position).sqrMagnitude <= 0.00000001f && Mathf.Abs(Quaternion.Dot(target.rotation, rotation)) >= 0.999999f) return;
            target.SetPositionAndRotation(position, rotation);
        }

        private static void SetLocalScaleIfChanged(Transform target, Vector3 scale)
        {
            if ((target.localScale - scale).sqrMagnitude <= 0.00000001f) return;
            target.localScale = scale;
        }
    }
}
