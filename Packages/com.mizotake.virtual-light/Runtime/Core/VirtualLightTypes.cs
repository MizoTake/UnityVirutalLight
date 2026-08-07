using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MizoTake.VirtualLight
{
    public enum VirtualLightType
    {
        Point = 0,
        Spot = 1,
        RectangleArea = 2,
        Directional = 3
    }

    public enum VirtualLightShape
    {
        Circle = 0,
        Rectangle = 1
    }

    [Flags]
    public enum VirtualLightFlags
    {
        None = 0,
        Enabled = 1 << 0,
        CastShadow = 1 << 1,
        AffectOpaque = 1 << 2,
        AffectTransparent = 1 << 3,
        AffectVolume = 1 << 4,
        Generated = 1 << 5,
        Static = 1 << 6,
        DebugSelected = 1 << 7,
        TwoSided = 1 << 8
    }

    public enum VirtualLightQuality
    {
        Low,
        Medium,
        High,
        Ultra
    }

    [Serializable]
    public readonly struct VirtualLightHandle : IEquatable<VirtualLightHandle>
    {
        internal VirtualLightHandle(int id, uint generation)
        {
            Id = id;
            Generation = generation;
        }

        public int Id { get; }
        public uint Generation { get; }
        public bool IsValid => Id > 0 && Generation > 0;

        public bool Equals(VirtualLightHandle other)
        {
            return Id == other.Id && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is VirtualLightHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Generation);
        }

        public static bool operator ==(VirtualLightHandle left, VirtualLightHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VirtualLightHandle left, VirtualLightHandle right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return IsValid ? $"VirtualLightHandle({Id}:{Generation})" : "VirtualLightHandle(Invalid)";
        }
    }

    [Serializable]
    public struct VirtualLightDescriptor : IEquatable<VirtualLightDescriptor>
    {
        public Vector3 Position;
        public Vector3 Direction;
        public Color LinearColor;
        public float Intensity;
        public float Radius;
        public float InnerConeAngle;
        public float OuterConeAngle;
        public float SpotPenumbraSharpness;
        public Vector2 AreaSize;
        public int AreaSampleCount;
        public float AreaRotation;
        public float OcclusionDistance;
        public bool TwoSided;
        public Texture2D GoboTexture;
        public VirtualLightType Type;
        public VirtualLightShape Shape;
        public VirtualLightFlags Flags;
        public int Priority;

        public static VirtualLightDescriptor Default => new VirtualLightDescriptor
        {
            Position = Vector3.zero,
            Direction = Vector3.forward,
            LinearColor = Color.white,
            Intensity = 1f,
            Radius = 5f,
            InnerConeAngle = 25f,
            OuterConeAngle = 40f,
            SpotPenumbraSharpness = 0f,
            AreaSize = Vector2.one,
            AreaSampleCount = 4,
            OcclusionDistance = -1f,
            Type = VirtualLightType.Point,
            Shape = VirtualLightShape.Circle,
            Flags = VirtualLightFlags.Enabled | VirtualLightFlags.AffectOpaque
        };

        public VirtualLightDescriptor Sanitized()
        {
            var sanitized = this;
            sanitized.Position = VirtualLightMath.FiniteOrZero(Position);
            sanitized.Direction = VirtualLightMath.NormalizeOrForward(Direction);
            sanitized.LinearColor = VirtualLightMath.FiniteColor(LinearColor);
            sanitized.Intensity = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(Intensity));
            sanitized.Radius = Mathf.Max(0f, VirtualLightMath.FiniteOrZero(Radius));
            sanitized.InnerConeAngle = Mathf.Clamp(VirtualLightMath.FiniteOrZero(InnerConeAngle), 0f, 179f);
            sanitized.OuterConeAngle = Mathf.Clamp(VirtualLightMath.FiniteOrZero(OuterConeAngle), 0f, 179f);
            if (sanitized.InnerConeAngle > sanitized.OuterConeAngle)
            {
                (sanitized.InnerConeAngle, sanitized.OuterConeAngle) = (sanitized.OuterConeAngle, sanitized.InnerConeAngle);
            }
            sanitized.SpotPenumbraSharpness = Mathf.Clamp01(VirtualLightMath.FiniteOrZero(SpotPenumbraSharpness));
            sanitized.AreaSize = new Vector2(Mathf.Max(0.01f, VirtualLightMath.FiniteOrZero(AreaSize.x)), Mathf.Max(0.01f, VirtualLightMath.FiniteOrZero(AreaSize.y)));
            sanitized.AreaSampleCount = VirtualLightMath.SanitizeAreaSampleCount(AreaSampleCount);
            sanitized.AreaRotation = VirtualLightMath.FiniteOrZero(AreaRotation);
            sanitized.Shape = VirtualLightMath.SanitizeShape(Shape);
            sanitized.OcclusionDistance = float.IsFinite(OcclusionDistance) ? Mathf.Clamp(OcclusionDistance, -1f, sanitized.Radius) : -1f;
            if (sanitized.TwoSided)
            {
                sanitized.Flags |= VirtualLightFlags.TwoSided;
            }
            else
            {
                sanitized.Flags &= ~VirtualLightFlags.TwoSided;
            }
            if (sanitized.Type != VirtualLightType.Directional && sanitized.Radius <= 0f)
            {
                sanitized.Flags &= ~VirtualLightFlags.Enabled;
            }
            return sanitized;
        }

        public bool Equals(VirtualLightDescriptor other)
        {
            return Position == other.Position && Direction == other.Direction && LinearColor == other.LinearColor && Intensity.Equals(other.Intensity) && Radius.Equals(other.Radius) && InnerConeAngle.Equals(other.InnerConeAngle) && OuterConeAngle.Equals(other.OuterConeAngle) && SpotPenumbraSharpness.Equals(other.SpotPenumbraSharpness) && AreaSize == other.AreaSize && AreaSampleCount == other.AreaSampleCount && AreaRotation.Equals(other.AreaRotation) && OcclusionDistance.Equals(other.OcclusionDistance) && TwoSided == other.TwoSided && GoboTexture == other.GoboTexture && Type == other.Type && Shape == other.Shape && Flags == other.Flags && Priority == other.Priority;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VirtualLightGpu
    {
        public Vector4 PositionRadius;
        public Vector4 ColorIntensity;
        public Vector4 DirectionType;
        public Vector4 ConeShadowFlags;
        public Vector4 AreaSizeParams;

        internal static VirtualLightGpu FromDescriptor(in VirtualLightDescriptor source)
        {
            var descriptor = source.Sanitized();
            return new VirtualLightGpu
            {
                PositionRadius = new Vector4(descriptor.Position.x, descriptor.Position.y, descriptor.Position.z, descriptor.Radius),
                ColorIntensity = new Vector4(descriptor.LinearColor.r, descriptor.LinearColor.g, descriptor.LinearColor.b, descriptor.Intensity),
                DirectionType = new Vector4(descriptor.Direction.x, descriptor.Direction.y, descriptor.Direction.z, (float)descriptor.Type),
                ConeShadowFlags = new Vector4(Mathf.Cos(descriptor.InnerConeAngle * Mathf.Deg2Rad * 0.5f), Mathf.Cos(descriptor.OuterConeAngle * Mathf.Deg2Rad * 0.5f), -1f, (float)(uint)descriptor.Flags),
                AreaSizeParams = PackShapeParameters(in descriptor)
            };
        }

        private static Vector4 PackShapeParameters(in VirtualLightDescriptor descriptor)
        {
            if (descriptor.Type == VirtualLightType.RectangleArea) return new Vector4(descriptor.AreaSize.x, descriptor.AreaSize.y, descriptor.AreaSampleCount, descriptor.AreaRotation * Mathf.Deg2Rad);
            if (VirtualLightMath.SupportsShape(descriptor.Type)) return new Vector4(descriptor.Type == VirtualLightType.Spot ? descriptor.SpotPenumbraSharpness : 0f, (float)descriptor.Shape, 0f, descriptor.AreaRotation * Mathf.Deg2Rad);
            return Vector4.zero;
        }
    }
}
