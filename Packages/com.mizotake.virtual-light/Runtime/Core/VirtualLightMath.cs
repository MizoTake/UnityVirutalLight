using UnityEngine;

namespace MizoTake.VirtualLight
{
    public static class VirtualLightMath
    {
        public static float EvaluateRangeAttenuation(float distanceToLight, float radius)
        {
            distanceToLight = Mathf.Max(0f, FiniteOrZero(distanceToLight));
            radius = Mathf.Max(0f, FiniteOrZero(radius));
            if (radius <= 0f || distanceToLight >= radius)
            {
                return 0f;
            }
            var normalizedDistance = distanceToLight / Mathf.Max(radius, 0.0001f);
            var window = Mathf.Clamp01(1f - Mathf.Pow(normalizedDistance, 4f));
            var inverseSquare = 1f / Mathf.Max(distanceToLight * distanceToLight, 0.0001f);
            return window * window * inverseSquare;
        }

        public static bool SupportsShape(VirtualLightType type)
        {
            return type == VirtualLightType.Point || type == VirtualLightType.Spot;
        }

        public static float EvaluatePunctualRangeDistance(Vector3 offsetFromLight, Vector3 lightDirection, float rotation, VirtualLightShape shape)
        {
            offsetFromLight = FiniteOrZero(offsetFromLight);
            shape = SanitizeShape(shape);
            if (shape == VirtualLightShape.Circle) return offsetFromLight.magnitude;
            GetLightBasis(lightDirection, rotation, out var right, out var up, out var forward);
            return Mathf.Max(Mathf.Abs(Vector3.Dot(offsetFromLight, right)), Mathf.Max(Mathf.Abs(Vector3.Dot(offsetFromLight, up)), Mathf.Abs(Vector3.Dot(offsetFromLight, forward))));
        }

        public static float ResolveVisibleDistance(float maximumDistance, float hitDistance, float surfaceOffset)
        {
            maximumDistance = Mathf.Max(0f, FiniteOrZero(maximumDistance));
            surfaceOffset = Mathf.Max(0f, FiniteOrZero(surfaceOffset));
            if (!float.IsFinite(hitDistance)) return maximumDistance;
            return Mathf.Clamp(hitDistance - surfaceOffset, 0f, maximumDistance);
        }

        public static float ResolveProjectedHitDistance(Vector3 origin, Vector3 direction, Vector3 hitPoint, float fallbackDistance, float maximumDistance)
        {
            origin = FiniteOrZero(origin);
            direction = NormalizeOrForward(direction);
            hitPoint = FiniteOrZero(hitPoint);
            fallbackDistance = Mathf.Max(0f, FiniteOrZero(fallbackDistance));
            maximumDistance = Mathf.Max(0f, FiniteOrZero(maximumDistance));
            var projectedDistance = Vector3.Dot(hitPoint - origin, direction);
            if (!float.IsFinite(projectedDistance) || projectedDistance <= 0f) projectedDistance = fallbackDistance;
            return Mathf.Clamp(projectedDistance, 0f, maximumDistance);
        }

        public static float EvaluateBeamRadius(float distance, float outerAngle)
        {
            distance = Mathf.Max(0f, FiniteOrZero(distance));
            outerAngle = Mathf.Clamp(FiniteOrZero(outerAngle), 0f, 179f);
            return distance * Mathf.Tan(outerAngle * Mathf.Deg2Rad * 0.5f);
        }

        public static bool TryEvaluateBeamFootprint(Vector3 origin, Vector3 direction, Vector3 referenceRight, Vector3 surfacePoint, Vector3 surfaceNormal, float range, float outerAngle, float sourceAperture, out VirtualLightBeamFootprint footprint)
        {
            footprint = default;
            origin = FiniteOrZero(origin);
            direction = NormalizeOrForward(direction);
            referenceRight = FiniteOrZero(referenceRight);
            surfacePoint = FiniteOrZero(surfacePoint);
            surfaceNormal = FiniteOrZero(surfaceNormal);
            range = Mathf.Max(0f, FiniteOrZero(range));
            outerAngle = Mathf.Clamp(FiniteOrZero(outerAngle), 0f, 179f);
            if (surfaceNormal.sqrMagnitude <= 0.000001f || range <= 0.000001f || outerAngle <= 0.0001f) return false;
            surfaceNormal.Normalize();
            if (Vector3.Dot(surfaceNormal, direction) > 0f) surfaceNormal = -surfaceNormal;
            var endRadius = EvaluateBeamRadius(range, outerAngle);
            if (endRadius <= 0.000001f) return false;
            sourceAperture = Mathf.Clamp(FiniteOrZero(sourceAperture), 0f, endRadius * 0.98f);
            var virtualApexExtension = sourceAperture > 0.000001f ? sourceAperture * range / Mathf.Max(endRadius - sourceAperture, 0.000001f) : 0f;
            if (!float.IsFinite(virtualApexExtension)) return false;
            var virtualOrigin = origin - direction * virtualApexExtension;
            var planeDenominator = Vector3.Dot(direction, surfaceNormal);
            if (Mathf.Abs(planeDenominator) <= 0.000001f) return false;
            var axialDistance = Vector3.Dot(surfacePoint - virtualOrigin, surfaceNormal) / planeDenominator;
            if (!float.IsFinite(axialDistance) || axialDistance <= 0f) return false;
            var coneTangent = endRadius / Mathf.Max(range + virtualApexExtension, 0.000001f);
            var incidenceCosine = Mathf.Clamp01(-planeDenominator);
            var surfaceSlope = Mathf.Sqrt(Mathf.Max(0f, 1f - incidenceCosine * incidenceCosine));
            var conicDenominator = incidenceCosine * incidenceCosine - coneTangent * coneTangent * surfaceSlope * surfaceSlope;
            if (!float.IsFinite(conicDenominator) || conicDenominator <= 0.000001f) return false;
            var commonRadius = coneTangent * axialDistance * incidenceCosine;
            var majorRadius = commonRadius / conicDenominator;
            var minorRadius = commonRadius / Mathf.Sqrt(conicDenominator);
            var centerShift = coneTangent * coneTangent * axialDistance * surfaceSlope / conicDenominator;
            if (!float.IsFinite(majorRadius) || !float.IsFinite(minorRadius) || !float.IsFinite(centerShift) || majorRadius <= 0f || minorRadius <= 0f) return false;
            var projectedDirection = Vector3.ProjectOnPlane(direction, surfaceNormal);
            Vector3 majorDirection;
            if (projectedDirection.sqrMagnitude > 0.000001f) majorDirection = projectedDirection.normalized;
            else
            {
                majorDirection = Vector3.ProjectOnPlane(referenceRight, surfaceNormal);
                if (majorDirection.sqrMagnitude <= 0.000001f)
                {
                    var seed = Mathf.Abs(surfaceNormal.y) < 0.99f ? Vector3.up : Vector3.right;
                    majorDirection = Vector3.Cross(seed, surfaceNormal);
                }
                majorDirection.Normalize();
            }
            var minorDirection = Vector3.Cross(surfaceNormal, majorDirection).normalized;
            var center = virtualOrigin + direction * axialDistance + majorDirection * centerShift;
            var rotation = Quaternion.LookRotation(surfaceNormal, minorDirection);
            footprint = new VirtualLightBeamFootprint(center, rotation, new Vector2(majorRadius * 2f, minorRadius * 2f), surfaceNormal, axialDistance);
            return true;
        }

        public static float EvaluateSpotAttenuation(Vector3 lightDirection, Vector3 directionFromLight, float innerConeAngle, float outerConeAngle)
        {
            return EvaluateSpotAttenuation(lightDirection, 0f, VirtualLightShape.Circle, directionFromLight, innerConeAngle, outerConeAngle);
        }

        public static float EvaluateSpotAttenuation(Vector3 lightDirection, float rotation, VirtualLightShape shape, Vector3 directionFromLight, float innerConeAngle, float outerConeAngle)
        {
            lightDirection = NormalizeOrForward(lightDirection);
            directionFromLight = NormalizeOrForward(directionFromLight);
            shape = SanitizeShape(shape);
            innerConeAngle = Mathf.Clamp(FiniteOrZero(innerConeAngle), 0f, 179f);
            outerConeAngle = Mathf.Clamp(FiniteOrZero(outerConeAngle), innerConeAngle, 179f);
            var innerCos = Mathf.Cos(innerConeAngle * Mathf.Deg2Rad * 0.5f);
            var outerCos = Mathf.Cos(outerConeAngle * Mathf.Deg2Rad * 0.5f);
            var angularCosine = Vector3.Dot(lightDirection, directionFromLight);
            if (shape == VirtualLightShape.Rectangle)
            {
                GetLightBasis(lightDirection, rotation, out var right, out var up, out var forward);
                var forwardProjection = Vector3.Dot(forward, directionFromLight);
                var lateralProjection = Mathf.Max(Mathf.Abs(Vector3.Dot(right, directionFromLight)), Mathf.Abs(Vector3.Dot(up, directionFromLight)));
                angularCosine = forwardProjection / Mathf.Max(Mathf.Sqrt(forwardProjection * forwardProjection + lateralProjection * lateralProjection), 0.0001f);
            }
            return Mathf.InverseLerp(outerCos, Mathf.Max(innerCos, outerCos + 0.0001f), angularCosine);
        }

        public static float EvaluateSpotPenumbraAttenuation(float angularAttenuation, float sharpness)
        {
            angularAttenuation = Mathf.Clamp01(FiniteOrZero(angularAttenuation));
            sharpness = Mathf.Clamp01(FiniteOrZero(sharpness));
            var standard = angularAttenuation * angularAttenuation;
            var focused = standard * standard;
            focused *= focused;
            return Mathf.Lerp(standard, focused, sharpness);
        }

        internal static Vector3 FiniteOrZero(Vector3 value)
        {
            return new Vector3(FiniteOrZero(value.x), FiniteOrZero(value.y), FiniteOrZero(value.z));
        }

        internal static float FiniteOrZero(float value)
        {
            return float.IsFinite(value) ? value : 0f;
        }

        internal static Vector3 NormalizeOrForward(Vector3 value)
        {
            value = FiniteOrZero(value);
            return value.sqrMagnitude > 0.000001f ? value.normalized : Vector3.forward;
        }

        internal static VirtualLightShape SanitizeShape(VirtualLightShape shape)
        {
            return shape == VirtualLightShape.Rectangle ? VirtualLightShape.Rectangle : VirtualLightShape.Circle;
        }

        internal static void GetLightBasis(Vector3 direction, float rotation, out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            forward = NormalizeOrForward(direction);
            var seed = Mathf.Abs(forward.y) < 0.99f ? Vector3.up : Vector3.right;
            var referenceRight = Vector3.Cross(seed, forward).normalized;
            var referenceUp = Vector3.Cross(forward, referenceRight).normalized;
            var radians = FiniteOrZero(rotation) * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            right = referenceRight * cosine + referenceUp * sine;
            up = referenceUp * cosine - referenceRight * sine;
        }

        internal static Color FiniteColor(Color value)
        {
            return new Color(Mathf.Max(0f, FiniteOrZero(value.r)), Mathf.Max(0f, FiniteOrZero(value.g)), Mathf.Max(0f, FiniteOrZero(value.b)), Mathf.Clamp01(FiniteOrZero(value.a)));
        }

        internal static int SanitizeAreaSampleCount(int requested)
        {
            if (requested <= 1) return 1;
            if (requested <= 2) return 2;
            if (requested <= 4) return 4;
            if (requested <= 8) return 8;
            return 16;
        }

        internal static Vector2Int GetAreaSampleGrid(int sampleCount, Vector2 areaSize)
        {
            sampleCount = SanitizeAreaSampleCount(sampleCount);
            var horizontal = FiniteOrZero(areaSize.x) >= FiniteOrZero(areaSize.y);
            return sampleCount switch
            {
                1 => new Vector2Int(1, 1),
                2 => horizontal ? new Vector2Int(2, 1) : new Vector2Int(1, 2),
                4 => new Vector2Int(2, 2),
                8 => horizontal ? new Vector2Int(4, 2) : new Vector2Int(2, 4),
                _ => new Vector2Int(4, 4)
            };
        }
    }
}
