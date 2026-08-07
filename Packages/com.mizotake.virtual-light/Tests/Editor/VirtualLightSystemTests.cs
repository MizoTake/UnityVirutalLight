using NUnit.Framework;
using UnityEngine;

namespace MizoTake.VirtualLight.Tests
{
    public sealed class VirtualLightSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            VirtualLightSystem.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            VirtualLightSystem.ResetForTests();
        }

        [Test]
        public void RegisterUpdateUnregister_RejectsStaleHandle()
        {
            var system = VirtualLightSystem.Current;
            var descriptor = VirtualLightDescriptor.Default;
            var first = system.Register(in descriptor);
            system.Unregister(first);
            var second = system.Register(in descriptor);

            descriptor.Intensity = 12f;
            system.Update(first, in descriptor);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(VirtualLightSystem.RegisteredCount, Is.EqualTo(1));
            Assert.That(VirtualLightSystem.TryGetDescriptor(second, out var stored), Is.True);
            Assert.That(stored.Intensity, Is.Not.EqualTo(12f));
        }

        [Test]
        public void SelectLights_PreservesPinnedAndHigherPriorityWhenCapacityIsExceeded()
        {
            var system = VirtualLightSystem.Current;
            var ordinary = VirtualLightDescriptor.Default;
            ordinary.Priority = 1;
            var high = VirtualLightDescriptor.Default;
            high.Priority = 100;
            var pinned = VirtualLightDescriptor.Default;
            pinned.Priority = -100;
            pinned.Flags |= VirtualLightFlags.Static;
            var ordinaryHandle = system.Register(in ordinary);
            var highHandle = system.Register(in high);
            var pinnedHandle = system.Register(in pinned);

            var selected = VirtualLightSystem.SelectHandlesForTests(Vector3.zero, 2);

            Assert.That(selected, Does.Contain(highHandle));
            Assert.That(selected, Does.Contain(pinnedHandle));
            CollectionAssert.DoesNotContain(selected, ordinaryHandle);
        }

        [Test]
        public void SelectLights_ReturnsEveryRegisteredLightBeyondLegacyFixedCaps()
        {
            const int lightCount = 300;
            var system = VirtualLightSystem.Current;
            var descriptor = VirtualLightDescriptor.Default;
            var registered = new VirtualLightHandle[lightCount];
            for (var index = 0; index < registered.Length; index++)
            {
                descriptor.Position = new Vector3(index, 0f, 0f);
                registered[index] = system.Register(in descriptor);
            }

            var selected = VirtualLightSystem.SelectHandlesForTests(Vector3.zero, lightCount);

            Assert.That(selected, Has.Length.EqualTo(lightCount));
            CollectionAssert.AreEquivalent(registered, selected);
        }

        [Test]
        public void SelectLights_DirectionalDoesNotRequireRadius()
        {
            var system = VirtualLightSystem.Current;
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Directional;
            descriptor.Radius = 0f;
            var handle = system.Register(in descriptor);

            var selected = VirtualLightSystem.SelectHandlesForTests(new Vector3(1000f, -500f, 250f), 1);

            Assert.That(selected, Is.EqualTo(new[] { handle }));
        }

        [Test]
        public void SelectLights_DirectionalContributionIsIndependentOfPosition()
        {
            var system = VirtualLightSystem.Current;
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Directional;
            descriptor.Radius = 0f;
            descriptor.Position = new Vector3(100000f, -50000f, 25000f);
            descriptor.Intensity = 2f;
            var stronger = system.Register(in descriptor);
            descriptor.Position = Vector3.zero;
            descriptor.Intensity = 1f;
            system.Register(in descriptor);

            var selected = VirtualLightSystem.SelectHandlesForTests(Vector3.zero, 1);

            Assert.That(selected, Is.EqualTo(new[] { stronger }));
        }

        [Test]
        public void SelectLights_UnsupportedDirectionalShadowIntentDoesNotChangePriority()
        {
            var system = VirtualLightSystem.Current;
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Directional;
            descriptor.Radius = 0f;
            descriptor.Intensity = 2f;
            var stronger = system.Register(in descriptor);
            descriptor.Intensity = 1f;
            descriptor.Flags |= VirtualLightFlags.CastShadow;
            system.Register(in descriptor);

            var selected = VirtualLightSystem.SelectHandlesForTests(Vector3.zero, 1);

            Assert.That(selected, Is.EqualTo(new[] { stronger }));
        }

        [Test]
        public void EvaluateRangeAttenuation_IsFiniteAndZeroOutsideRadius()
        {
            Assert.That(VirtualLightMath.EvaluateRangeAttenuation(5f, 5f), Is.Zero);
            Assert.That(VirtualLightMath.EvaluateRangeAttenuation(6f, 5f), Is.Zero);
            Assert.That(float.IsFinite(VirtualLightMath.EvaluateRangeAttenuation(0f, 0f)), Is.True);
        }

        [Test]
        public void EvaluateRangeAttenuation_UsesWindowedInverseSquareFalloff()
        {
            const float distance = 2f;
            const float radius = 4f;
            var normalizedDistance = distance / radius;
            var window = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Pow(normalizedDistance, 4f)), 2f);
            var expected = window / (distance * distance);

            Assert.That(VirtualLightMath.EvaluateRangeAttenuation(distance, radius), Is.EqualTo(expected).Within(0.000001f));
        }

        [Test]
        public void EvaluatePunctualRangeDistance_RectangleUsesRotatedBoxBoundary()
        {
            var offset = new Vector3(1f, 1f, 1f);

            var circleDistance = VirtualLightMath.EvaluatePunctualRangeDistance(offset, Vector3.forward, 0f, VirtualLightShape.Circle);
            var rectangleDistance = VirtualLightMath.EvaluatePunctualRangeDistance(offset, Vector3.forward, 0f, VirtualLightShape.Rectangle);
            var rotatedRectangleDistance = VirtualLightMath.EvaluatePunctualRangeDistance(new Vector3(Mathf.Sqrt(2f), 0f, 0f), Vector3.forward, 45f, VirtualLightShape.Rectangle);

            Assert.That(circleDistance, Is.EqualTo(Mathf.Sqrt(3f)).Within(0.0001f));
            Assert.That(rectangleDistance, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(rotatedRectangleDistance, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void EvaluateSpotAttenuation_RectangleIncludesSquareCornerOutsideCircle()
        {
            var directionFromLight = new Vector3(0.5f, 0.5f, 1f).normalized;

            var circle = VirtualLightMath.EvaluateSpotAttenuation(Vector3.forward, 0f, VirtualLightShape.Circle, directionFromLight, 40f, 60f);
            var rectangle = VirtualLightMath.EvaluateSpotAttenuation(Vector3.forward, 0f, VirtualLightShape.Rectangle, directionFromLight, 40f, 60f);

            Assert.That(circle, Is.Zero);
            Assert.That(rectangle, Is.GreaterThan(0f));
        }

        [TestCase(VirtualLightShape.Circle)]
        [TestCase(VirtualLightShape.Rectangle)]
        public void EvaluateSpotAttenuation_ReturnsGpuCompatibleLinearAngularRamp(VirtualLightShape shape)
        {
            const float innerAngle = 40f;
            const float outerAngle = 60f;
            var innerCosine = Mathf.Cos(innerAngle * Mathf.Deg2Rad * 0.5f);
            var outerCosine = Mathf.Cos(outerAngle * Mathf.Deg2Rad * 0.5f);
            var angularCosine = Mathf.Lerp(outerCosine, innerCosine, 0.25f);
            var directionFromLight = new Vector3(Mathf.Sqrt(1f - angularCosine * angularCosine), 0f, angularCosine);

            var attenuation = VirtualLightMath.EvaluateSpotAttenuation(Vector3.forward, 0f, shape, directionFromLight, innerAngle, outerAngle);

            Assert.That(attenuation, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(VirtualLightMath.EvaluateSpotPenumbraAttenuation(attenuation, 0f), Is.EqualTo(0.0625f).Within(0.0001f));
        }

        [TestCase(0.5f, 0f, 0.25f)]
        [TestCase(0.5f, 1f, 0.00390625f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(0f, 1f, 0f)]
        public void EvaluateSpotPenumbraAttenuation_FocusesTheVisibleEdgeWithoutMovingInnerOrOuterBoundaries(float angularAttenuation, float sharpness, float expected)
        {
            Assert.That(VirtualLightMath.EvaluateSpotPenumbraAttenuation(angularAttenuation, sharpness), Is.EqualTo(expected).Within(0.000001f));
        }

        [TestCase(5f, float.PositiveInfinity, 0.05f, 5f)]
        [TestCase(5f, 2f, 0.05f, 1.95f)]
        [TestCase(5f, 8f, 0.05f, 5f)]
        [TestCase(5f, -1f, 0.05f, 0f)]
        public void ResolveVisibleDistance_ClampsHitAndSurfaceOffset(float maximumDistance, float hitDistance, float surfaceOffset, float expected)
        {
            Assert.That(VirtualLightMath.ResolveVisibleDistance(maximumDistance, hitDistance, surfaceOffset), Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void ResolveProjectedHitDistance_UsesSurfacePointInsteadOfSphereCenterTravel()
        {
            var projected = VirtualLightMath.ResolveProjectedHitDistance(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 4f), 3f, 6f);

            Assert.That(projected, Is.EqualTo(4f).Within(0.0001f));
        }

        [TestCase(5f, 10f, 0.4374433f)]
        [TestCase(0f, 40f, 0f)]
        [TestCase(-2f, 40f, 0f)]
        public void EvaluateBeamRadius_UsesSpotOuterAngle(float distance, float outerAngle, float expected)
        {
            Assert.That(VirtualLightMath.EvaluateBeamRadius(distance, outerAngle), Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void EvaluateBeamRadius_RepairsNonFiniteInputs()
        {
            Assert.That(VirtualLightMath.EvaluateBeamRadius(float.NaN, float.PositiveInfinity), Is.Zero);
            Assert.That(float.IsFinite(VirtualLightMath.EvaluateBeamRadius(5f, 179f)), Is.True);
        }

        [Test]
        public void BeamFootprint_FaceOnPlaneMatchesFiniteSourceFrustum()
        {
            var success = VirtualLightMath.TryEvaluateBeamFootprint(Vector3.zero, Vector3.forward, Vector3.right, new Vector3(0f, 0f, 5f), Vector3.back, 10f, 20f, 0.08f, out var footprint);
            var endRadius = VirtualLightMath.EvaluateBeamRadius(10f, 20f);
            var expectedRadius = Mathf.Lerp(0.08f, endRadius, 0.5f);

            Assert.That(success, Is.True);
            Assert.That(Vector3.Distance(footprint.Center, new Vector3(0f, 0f, 5f)), Is.LessThan(0.00001f));
            Assert.That(Vector2.Distance(footprint.Diameter, Vector2.one * expectedRadius * 2f), Is.LessThan(0.00001f));
            Assert.That(Vector3.Dot(footprint.Rotation * Vector3.forward, Vector3.back), Is.GreaterThan(0.99999f));
            Assert.That(footprint.AspectRatio, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void BeamFootprint_ObliquePlaneUsesExactEllipseCenterAndAxes()
        {
            var surfaceNormal = Quaternion.AngleAxis(45f, Vector3.up) * Vector3.back;
            var success = VirtualLightMath.TryEvaluateBeamFootprint(Vector3.zero, Vector3.forward, Vector3.right, new Vector3(0f, 0f, 5f), surfaceNormal, 10f, 20f, 0f, out var footprint);
            var tangent = Mathf.Tan(10f * Mathf.Deg2Rad);
            var incidence = Mathf.Abs(Vector3.Dot(surfaceNormal, Vector3.forward));
            var surfaceSlope = Mathf.Sqrt(1f - incidence * incidence);
            var denominator = incidence * incidence - tangent * tangent * surfaceSlope * surfaceSlope;
            var expectedMajorRadius = tangent * 5f * incidence / denominator;
            var expectedMinorRadius = tangent * 5f * incidence / Mathf.Sqrt(denominator);
            var expectedCenterShift = tangent * tangent * 5f * surfaceSlope / denominator;
            var expectedMajorDirection = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal).normalized;

            Assert.That(success, Is.True);
            Assert.That(footprint.Diameter.x, Is.EqualTo(expectedMajorRadius * 2f).Within(0.00001f));
            Assert.That(footprint.Diameter.y, Is.EqualTo(expectedMinorRadius * 2f).Within(0.00001f));
            Assert.That(Vector3.Distance(footprint.Center, new Vector3(0f, 0f, 5f) + expectedMajorDirection * expectedCenterShift), Is.LessThan(0.00001f));
            Assert.That(Mathf.Abs(Vector3.Dot(footprint.Rotation * Vector3.right, expectedMajorDirection)), Is.GreaterThan(0.99999f));
        }

        [Test]
        public void BeamFootprint_NormalDirectionDoesNotChangePhysicalEllipse()
        {
            var surfaceNormal = Quaternion.AngleAxis(35f, Vector3.up) * Vector3.back;
            var forwardSuccess = VirtualLightMath.TryEvaluateBeamFootprint(Vector3.zero, Vector3.forward, Vector3.right, new Vector3(0f, 0f, 4f), surfaceNormal, 8f, 16f, 0.05f, out var forwardFootprint);
            var reversedSuccess = VirtualLightMath.TryEvaluateBeamFootprint(Vector3.zero, Vector3.forward, Vector3.right, new Vector3(0f, 0f, 4f), -surfaceNormal, 8f, 16f, 0.05f, out var reversedFootprint);

            Assert.That(forwardSuccess, Is.True);
            Assert.That(reversedSuccess, Is.True);
            Assert.That(Vector3.Distance(forwardFootprint.Center, reversedFootprint.Center), Is.LessThan(0.00001f));
            Assert.That(Vector2.Distance(forwardFootprint.Diameter, reversedFootprint.Diameter), Is.LessThan(0.00001f));
            Assert.That(Quaternion.Angle(forwardFootprint.Rotation, reversedFootprint.Rotation), Is.LessThan(0.001f));
        }

        [Test]
        public void BeamFootprint_GrazingPlaneRejectsUnboundedConic()
        {
            var surfaceNormal = Quaternion.AngleAxis(85f, Vector3.up) * Vector3.back;

            var success = VirtualLightMath.TryEvaluateBeamFootprint(Vector3.zero, Vector3.forward, Vector3.right, new Vector3(0f, 0f, 5f), surfaceNormal, 10f, 20f, 0f, out var footprint);

            Assert.That(success, Is.False);
            Assert.That(footprint.IsValid, Is.False);
        }

        [Test]
        public void ShadowMath_BuildsFiniteSpotViewProjection()
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Spot;
            descriptor.Position = new Vector3(1f, 4f, -2f);
            descriptor.Direction = new Vector3(0.1f, -0.8f, 0.5f).normalized;
            descriptor.OuterConeAngle = 10f;
            descriptor.Radius = 12f;

            var matrix = VirtualLightShadowMath.BuildViewProjection(descriptor);

            for (var row = 0; row < 4; row++) for (var column = 0; column < 4; column++) Assert.That(float.IsFinite(matrix[row, column]), Is.True);
        }

        [Test]
        public void ShadowMath_RectangleSpotViewFollowsTransformRoll()
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Spot;
            descriptor.Shape = VirtualLightShape.Rectangle;
            descriptor.Direction = Vector3.forward;
            descriptor.AreaRotation = 45f;
            VirtualLightMath.GetLightBasis(descriptor.Direction, descriptor.AreaRotation, out var right, out var up, out _);

            var view = VirtualLightShadowMath.BuildView(descriptor);
            var viewRight = view.MultiplyVector(right);
            var viewUp = view.MultiplyVector(up);

            Assert.That(viewRight.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(viewRight.y, Is.Zero.Within(0.0001f));
            Assert.That(viewUp.x, Is.Zero.Within(0.0001f));
            Assert.That(viewUp.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ShadowMath_SourceApertureProjectionMatchesBeamFrustumAtSourceAndRange()
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Spot;
            descriptor.Position = new Vector3(1f, 2f, 3f);
            descriptor.Direction = new Vector3(0.2f, -0.3f, 0.9f).normalized;
            descriptor.Radius = 5f;
            descriptor.InnerConeAngle = 6f;
            descriptor.OuterConeAngle = 10f;
            const float sourceAperture = 0.08f;
            var baseRadius = VirtualLightMath.EvaluateBeamRadius(descriptor.Radius, descriptor.OuterConeAngle);

            var expanded = VirtualLightShadowMath.ExpandProjectionForSourceAperture(descriptor, sourceAperture);
            var extension = Vector3.Dot(descriptor.Position - expanded.Position, descriptor.Direction);
            var expandedHalfAngleTangent = Mathf.Tan(expanded.OuterConeAngle * Mathf.Deg2Rad * 0.5f);

            Assert.That(extension, Is.GreaterThan(0f));
            Assert.That(expanded.Radius, Is.EqualTo(descriptor.Radius + extension).Within(0.00001f));
            Assert.That(extension * expandedHalfAngleTangent, Is.EqualTo(sourceAperture).Within(0.00001f));
            Assert.That(expanded.Radius * expandedHalfAngleTangent, Is.EqualTo(baseRadius).Within(0.00001f));
            Assert.That(expanded.OuterConeAngle, Is.LessThan(descriptor.OuterConeAngle));
        }

        [Test]
        public void ShadowMath_ProjectsSpotAxisToCenterAndOffAxisPointsSymmetrically()
        {
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Spot;
            descriptor.Position = new Vector3(1.3f, 4.2f, -2.4f);
            descriptor.Direction = new Vector3(0.23f, -0.72f, 0.65f).normalized;
            descriptor.OuterConeAngle = 24f;
            descriptor.Radius = 12f;
            var stableUp = Mathf.Abs(Vector3.Dot(descriptor.Direction, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
            var rotation = Quaternion.LookRotation(descriptor.Direction, stableUp);
            var center = descriptor.Position + descriptor.Direction * 5f;
            var right = rotation * Vector3.right * 0.4f;
            var up = rotation * Vector3.up * 0.4f;
            var matrix = VirtualLightShadowMath.BuildViewProjection(descriptor);

            var centerClip = matrix * new Vector4(center.x, center.y, center.z, 1f);
            var rightClip = matrix * new Vector4(center.x + right.x, center.y + right.y, center.z + right.z, 1f);
            var leftClip = matrix * new Vector4(center.x - right.x, center.y - right.y, center.z - right.z, 1f);
            var upClip = matrix * new Vector4(center.x + up.x, center.y + up.y, center.z + up.z, 1f);
            var downClip = matrix * new Vector4(center.x - up.x, center.y - up.y, center.z - up.z, 1f);
            var centerUv = new Vector2(centerClip.x / centerClip.w, centerClip.y / centerClip.w) * 0.5f + Vector2.one * 0.5f;
            var rightUv = new Vector2(rightClip.x / rightClip.w, rightClip.y / rightClip.w) * 0.5f + Vector2.one * 0.5f;
            var leftUv = new Vector2(leftClip.x / leftClip.w, leftClip.y / leftClip.w) * 0.5f + Vector2.one * 0.5f;
            var upUv = new Vector2(upClip.x / upClip.w, upClip.y / upClip.w) * 0.5f + Vector2.one * 0.5f;
            var downUv = new Vector2(downClip.x / downClip.w, downClip.y / downClip.w) * 0.5f + Vector2.one * 0.5f;

            Assert.That(centerClip.w, Is.GreaterThan(0f));
            Assert.That(Vector2.Distance(centerUv, Vector2.one * 0.5f), Is.LessThan(0.0001f));
            Assert.That(rightUv.x, Is.GreaterThan(centerUv.x));
            Assert.That(Mathf.Abs((rightUv.x + leftUv.x) * 0.5f - centerUv.x), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs((upUv.y + downUv.y) * 0.5f - centerUv.y), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(upUv.y - downUv.y), Is.GreaterThan(0.001f));
        }
    }
}
