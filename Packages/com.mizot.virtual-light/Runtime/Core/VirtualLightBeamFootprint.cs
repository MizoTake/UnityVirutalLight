using UnityEngine;

namespace MizoTake.VirtualLight
{
    public readonly struct VirtualLightBeamFootprint
    {
        internal VirtualLightBeamFootprint(Vector3 center, Quaternion rotation, Vector2 diameter, Vector3 surfaceNormal, float axialDistance)
        {
            Center = center;
            Rotation = rotation;
            Diameter = diameter;
            SurfaceNormal = surfaceNormal;
            AxialDistance = axialDistance;
            IsValid = true;
        }

        public Vector3 Center { get; }
        public Quaternion Rotation { get; }
        public Vector2 Diameter { get; }
        public Vector3 SurfaceNormal { get; }
        public float AxialDistance { get; }
        public bool IsValid { get; }
        public float AspectRatio => IsValid && Diameter.y > 0.000001f ? Diameter.x / Diameter.y : 0f;
    }
}
