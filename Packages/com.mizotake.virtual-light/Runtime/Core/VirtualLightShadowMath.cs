using UnityEngine;

namespace MizoTake.VirtualLight
{
    internal static class VirtualLightShadowMath
    {
        private const float PointFaceFieldOfView = 94f;

        internal static int GetSliceCount(in VirtualLightDescriptor source)
        {
            return source.Type switch
            {
                VirtualLightType.Point => 6,
                VirtualLightType.Spot => 1,
                VirtualLightType.RectangleArea => 2,
                VirtualLightType.Directional => 1,
                _ => 0
            };
        }

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
            if (descriptor.Shape == VirtualLightShape.Rectangle) VirtualLightMath.GetLightBasis(direction, descriptor.AreaRotation, out _, out up, out direction);
            return BuildView(descriptor.Position, direction, up);
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

        internal static Matrix4x4 BuildPointView(in VirtualLightDescriptor source, CubemapFace face)
        {
            var descriptor = source.Sanitized();
            GetPointFaceBasis(face, out var direction, out var up);
            return BuildView(descriptor.Position, direction, up);
        }

        internal static Matrix4x4 BuildPointProjection(in VirtualLightDescriptor source)
        {
            var descriptor = source.Sanitized();
            var nearPlane = BuildNearPlane(descriptor.Radius);
            return Matrix4x4.Perspective(PointFaceFieldOfView, 1f, nearPlane, Mathf.Max(descriptor.Radius, nearPlane + 0.01f));
        }

        internal static Matrix4x4 BuildAreaView(in VirtualLightDescriptor source, bool backFace)
        {
            var descriptor = source.Sanitized();
            VirtualLightMath.GetLightBasis(descriptor.Direction, descriptor.AreaRotation, out _, out var up, out var forward);
            return BuildView(descriptor.Position, backFace ? -forward : forward, up);
        }

        internal static Matrix4x4 BuildAreaProjection(in VirtualLightDescriptor source)
        {
            var descriptor = source.Sanitized();
            var nearPlane = BuildNearPlane(descriptor.Radius);
            var halfWidth = Mathf.Max(descriptor.AreaSize.x * 0.5f + descriptor.Radius, 0.01f);
            var halfHeight = Mathf.Max(descriptor.AreaSize.y * 0.5f + descriptor.Radius, 0.01f);
            return Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight, nearPlane, Mathf.Max(descriptor.Radius, nearPlane + 0.01f));
        }

        internal static Matrix4x4 BuildDirectionalView(in VirtualLightDescriptor source, Camera camera, out Vector3 depthOrigin)
        {
            var descriptor = source.Sanitized();
            var direction = VirtualLightMath.NormalizeOrForward(descriptor.Direction);
            var range = Mathf.Max(descriptor.Radius, 0.01f);
            var center = camera != null ? camera.transform.position + camera.transform.forward * range * 0.5f : descriptor.Position;
            depthOrigin = center - direction * range * 0.5f;
            var up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
            return BuildView(depthOrigin, direction, up);
        }

        internal static Matrix4x4 BuildDirectionalProjection(in VirtualLightDescriptor source)
        {
            var descriptor = source.Sanitized();
            var range = Mathf.Max(descriptor.Radius, 0.01f);
            var nearPlane = BuildNearPlane(range);
            return Matrix4x4.Ortho(-range, range, -range, range, nearPlane, range + nearPlane);
        }

        internal static Vector3 GetPointFaceDirection(CubemapFace face)
        {
            GetPointFaceBasis(face, out var direction, out _);
            return direction;
        }

        private static Matrix4x4 BuildView(Vector3 position, Vector3 direction, Vector3 up)
        {
            var rotation = Quaternion.LookRotation(direction, up);
            return Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
        }

        private static float BuildNearPlane(float range)
        {
            return Mathf.Min(0.1f, Mathf.Max(range * 0.01f, 0.01f));
        }

        private static void GetPointFaceBasis(CubemapFace face, out Vector3 direction, out Vector3 up)
        {
            switch (face)
            {
                case CubemapFace.PositiveX:
                    direction = Vector3.right;
                    up = Vector3.down;
                    return;
                case CubemapFace.NegativeX:
                    direction = Vector3.left;
                    up = Vector3.down;
                    return;
                case CubemapFace.PositiveY:
                    direction = Vector3.up;
                    up = Vector3.forward;
                    return;
                case CubemapFace.NegativeY:
                    direction = Vector3.down;
                    up = Vector3.back;
                    return;
                case CubemapFace.PositiveZ:
                    direction = Vector3.forward;
                    up = Vector3.down;
                    return;
                case CubemapFace.NegativeZ:
                    direction = Vector3.back;
                    up = Vector3.down;
                    return;
                default:
                    direction = Vector3.forward;
                    up = Vector3.down;
                    return;
            }
        }
    }
}
