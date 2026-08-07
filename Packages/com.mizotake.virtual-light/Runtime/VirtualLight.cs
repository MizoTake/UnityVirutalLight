using UnityEngine;

namespace MizoTake.VirtualLight
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Virtual Light")]
    public sealed class VirtualLight : MonoBehaviour
    {
        [SerializeField] private VirtualLightType type = VirtualLightType.Point;
        [SerializeField, Tooltip("Selects a circular/spherical Point or Spot influence, or a Transform-aligned rectangular box/pyramid influence.")] private VirtualLightShape shape = VirtualLightShape.Circle;
        [SerializeField, ColorUsage(true, true)] private Color color = Color.white;
        [SerializeField, Min(0f)] private float intensity = 1f;
        [SerializeField, Min(0.01f)] private float range = 5f;
        [SerializeField, Range(0f, 179f)] private float innerAngle = 25f;
        [SerializeField, Range(0f, 179f)] private float outerAngle = 40f;
        [SerializeField, Range(0f, 1f), Tooltip("Keeps the Inner Angle fully lit while concentrating the visible surface penumbra toward it. Zero preserves the standard squared falloff; one uses a focused eighth-power falloff.")] private float spotPenumbraSharpness;
        [SerializeField] private Vector2 areaSize = Vector2.one;
        [SerializeField, Range(1, 16)] private int areaSampleCount = 4;
        [SerializeField] private bool twoSided;
        [SerializeField] private bool castShadow;
        [SerializeField] private bool affectOpaque = true;
        [SerializeField, HideInInspector] private float occlusionDistance = -1f;
        [SerializeField] private bool staticPriority;
        [SerializeField] private int priority;
        [SerializeField] private bool alwaysShowGizmo = true;
        [SerializeField] private bool showInfluenceVolume = true;
        [SerializeField] private bool showSamplePoints;
        private VirtualLightHandle handle;
        private VirtualLightDescriptor lastDescriptor;
        private bool hasDescriptor;

        public VirtualLightHandle Handle => handle;
        public VirtualLightType Type { get => type; set { type = value; Synchronize(); } }
        public VirtualLightShape Shape { get => shape; set { shape = VirtualLightMath.SanitizeShape(value); Synchronize(); } }
        public Color Color { get => color; set { color = value; Synchronize(); } }
        public float Intensity { get => intensity; set { intensity = Mathf.Max(0f, value); Synchronize(); } }
        public float Range { get => range; set { range = Mathf.Max(0.01f, value); Synchronize(); } }
        public float InnerAngle { get => innerAngle; set { innerAngle = value; NormalizeSerializedValues(); Synchronize(); } }
        public float OuterAngle { get => outerAngle; set { outerAngle = value; NormalizeSerializedValues(); Synchronize(); } }
        public float SpotPenumbraSharpness { get => spotPenumbraSharpness; set { spotPenumbraSharpness = Mathf.Clamp01(VirtualLightMath.FiniteOrZero(value)); Synchronize(); } }
        public Vector2 AreaSize { get => areaSize; set { areaSize = value; NormalizeSerializedValues(); Synchronize(); } }
        public int AreaSampleCount { get => areaSampleCount; set { areaSampleCount = VirtualLightMath.SanitizeAreaSampleCount(value); Synchronize(); } }
        public bool TwoSided { get => twoSided; set { twoSided = value; Synchronize(); } }
        public bool CastShadow { get => castShadow; set { castShadow = value; Synchronize(); } }
        public bool AffectOpaque { get => affectOpaque; set { if (affectOpaque == value) return; affectOpaque = value; Synchronize(); } }
        public float OcclusionDistance { get => occlusionDistance; set { occlusionDistance = float.IsFinite(value) ? Mathf.Clamp(value, -1f, range) : -1f; Synchronize(); } }
        public int Priority { get => priority; set { priority = value; Synchronize(); } }
        public bool AlwaysShowGizmo => alwaysShowGizmo;
        public bool ShowInfluenceVolume => showInfluenceVolume;
        public bool ShowSamplePoints => showSamplePoints;

        public VirtualLightDescriptor Descriptor
        {
            get
            {
                var flags = VirtualLightFlags.Enabled;
                if (affectOpaque) flags |= VirtualLightFlags.AffectOpaque;
                if (castShadow) flags |= VirtualLightFlags.CastShadow;
                if (staticPriority) flags |= VirtualLightFlags.Static;
                if (twoSided) flags |= VirtualLightFlags.TwoSided;
                return new VirtualLightDescriptor
                {
                    Position = transform.position,
                    Direction = transform.forward,
                    LinearColor = color.linear,
                    Intensity = intensity,
                    Radius = range,
                    InnerConeAngle = innerAngle,
                    OuterConeAngle = outerAngle,
                    SpotPenumbraSharpness = spotPenumbraSharpness,
                    AreaSize = areaSize,
                    AreaSampleCount = areaSampleCount,
                    AreaRotation = CalculateAreaRotation(),
                    OcclusionDistance = occlusionDistance,
                    TwoSided = twoSided,
                    Type = type,
                    Shape = shape,
                    Flags = flags,
                    Priority = priority
                }.Sanitized();
            }
        }

        private void OnEnable()
        {
            NormalizeSerializedValues();
            var descriptor = Descriptor;
            handle = VirtualLightSystem.Current.Register(in descriptor);
            lastDescriptor = descriptor;
            hasDescriptor = true;
        }

        private void Update()
        {
            Synchronize();
        }

        private void OnDisable()
        {
            VirtualLightSystem.Current.Unregister(handle);
            handle = default;
            hasDescriptor = false;
        }

        private void OnValidate()
        {
            NormalizeSerializedValues();
            Synchronize();
        }

        private void Synchronize()
        {
            if (!isActiveAndEnabled || !handle.IsValid) return;
            var descriptor = Descriptor;
            if (hasDescriptor && lastDescriptor.Equals(descriptor)) return;
            VirtualLightSystem.Current.Update(handle, in descriptor);
            lastDescriptor = descriptor;
            hasDescriptor = true;
        }

        private void NormalizeSerializedValues()
        {
            intensity = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(intensity));
            range = Mathf.Max(0.01f, VirtualLightMath.FiniteOrZero(range));
            innerAngle = Mathf.Clamp(VirtualLightMath.FiniteOrZero(innerAngle), 0f, 179f);
            outerAngle = Mathf.Clamp(VirtualLightMath.FiniteOrZero(outerAngle), 0f, 179f);
            if (innerAngle > outerAngle) (innerAngle, outerAngle) = (outerAngle, innerAngle);
            spotPenumbraSharpness = Mathf.Clamp01(VirtualLightMath.FiniteOrZero(spotPenumbraSharpness));
            areaSize = new Vector2(Mathf.Max(0.01f, VirtualLightMath.FiniteOrZero(areaSize.x)), Mathf.Max(0.01f, VirtualLightMath.FiniteOrZero(areaSize.y)));
            areaSampleCount = VirtualLightMath.SanitizeAreaSampleCount(areaSampleCount);
            occlusionDistance = float.IsFinite(occlusionDistance) ? Mathf.Clamp(occlusionDistance, -1f, range) : -1f;
            shape = VirtualLightMath.SanitizeShape(shape);
        }

        private float CalculateAreaRotation()
        {
            var forward = VirtualLightMath.NormalizeOrForward(transform.forward);
            var seed = Mathf.Abs(forward.y) < 0.99f ? Vector3.up : Vector3.right;
            var referenceRight = Vector3.Cross(seed, forward).normalized;
            return Vector3.SignedAngle(referenceRight, transform.right, forward);
        }
    }
}
