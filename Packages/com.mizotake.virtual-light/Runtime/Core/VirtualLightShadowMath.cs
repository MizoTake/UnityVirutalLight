using UnityEngine;

namespace MizoTake.VirtualLight
{
    internal static class VirtualLightShadowMath
    {
        internal static VirtualLightDescriptor ExpandProjectionForSourceAperture(in VirtualLightDescriptor source, float sourceAperture)
        {
            var descriptor = source.Sanitized();
            if (descriptor.Type != VirtualLightType.Spot) return descriptor;
            var range = descriptor.Radius;
            var baseRadius = VirtualLightMath.EvaluateBeamRadius(range, descriptor.OuterConeAngle);
            var aperture = Mathf.Clamp(VirtualLightMath.FiniteOrZero(sourceAperture), 0f, baseRadius * 0.98f);
            if (range <= 0f || baseRadius <= 0.0001f || aperture <= 0.0001f) return descriptor;
            var extension = aperture * range / Mathf.Max(baseRadius - aperture, 0.0001f);
            if (!float.IsFinite(extension) || extension <= 0f) return descriptor;
            descriptor.Position -= descriptor.Direction * extension;
            descriptor.Radius = range + extension;
            descriptor.OuterConeAngle = Mathf.Atan2(baseRadius, descriptor.Radius) * Mathf.Rad2Deg * 2f;
            descriptor.InnerConeAngle = Mathf.Min(descriptor.InnerConeAngle, descriptor.OuterConeAngle);
            return descriptor;
        }

        internal static Matrix4x4 BuildView(in VirtualLightDescriptor descriptor)
        {
            var direction = VirtualLightMath.NormalizeOrForward(descriptor.Direction);
            var up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
            var rotation = Quaternion.LookRotation(direction, up);
            return Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(descriptor.Position, rotation, Vector3.one).inverse;
        }

        internal static Matrix4x4 BuildProjection(in VirtualLightDescriptor descriptor)
        {
            var nearPlane = Mathf.Min(0.1f, Mathf.Max(descriptor.Radius * 0.01f, 0.01f));
            return Matrix4x4.Perspective(Mathf.Clamp(descriptor.OuterConeAngle, 0.1f, 179f), 1f, nearPlane, Mathf.Max(descriptor.Radius, nearPlane + 0.01f));
        }

        internal static Matrix4x4 BuildViewProjection(in VirtualLightDescriptor descriptor)
        {
            return GL.GetGPUProjectionMatrix(BuildProjection(descriptor), false) * BuildView(descriptor);
        }
    }
}
