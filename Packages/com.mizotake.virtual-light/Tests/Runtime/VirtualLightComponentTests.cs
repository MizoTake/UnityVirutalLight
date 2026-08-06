using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MizoTake.VirtualLight.Tests
{
    public sealed class VirtualLightComponentTests
    {
        [UnityTest]
        public IEnumerator Component_RegistersUpdatesAndUnregisters()
        {
            VirtualLightSystem.ResetForTests();
            var gameObject = new GameObject("Virtual Light Test");
            var light = gameObject.AddComponent<VirtualLight>();
            yield return null;

            Assert.That(VirtualLightSystem.RegisteredCount, Is.EqualTo(1));
            gameObject.transform.position = new Vector3(1f, 2f, 3f);
            yield return null;
            Assert.That(VirtualLightSystem.TryGetDescriptor(light.Handle, out var descriptor), Is.True);
            Assert.That(descriptor.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));

            Object.Destroy(gameObject);
            yield return null;
            Assert.That(VirtualLightSystem.RegisteredCount, Is.Zero);
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator CameraRender_PointLightIncreasesReceiverLuminance()
        {
            VirtualLightSystem.ResetForTests();
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            var computeShader = Resources.Load<ComputeShader>("VirtualLightTileCulling");
            Assert.That(computeShader, Is.Not.Null);
            Assert.That(computeShader.HasKernel("CullTiles"), Is.True);
            var cameraObject = new GameObject("Virtual Light Render Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -4f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            var renderTexture = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Quad);
            receiver.transform.position = Vector3.zero;
            receiver.transform.localScale = Vector3.one * 2f;
            var shader = Shader.Find("MizoTake/Virtual Light/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_ReceiveStandardLighting", 0f);
            material.EnableKeyword("_RECEIVE_STANDARD_LIGHTING_OFF");
            receiver.GetComponent<Renderer>().sharedMaterial = material;
            yield return null;
            var baseline = RenderCenter(camera, renderTexture);
            var lightObject = new GameObject("Virtual Light Render Test Point");
            lightObject.transform.position = new Vector3(0f, 0f, -2f);
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Color = Color.red;
            virtualLight.Intensity = 30f;
            virtualLight.Range = 5f;
            yield return null;
            var lit = RenderCenter(camera, renderTexture);

            Assert.That(lit.r, Is.GreaterThan(baseline.r + 0.05f));
            Object.Destroy(lightObject);
            Object.Destroy(receiver);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator CameraRender_StandardLightingOptionControlsUrpMainLightContribution()
        {
            VirtualLightSystem.ResetForTests();
            var cameraObject = new GameObject("Standard Lighting Option Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -4f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            var renderTexture = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Quad);
            receiver.transform.position = Vector3.zero;
            receiver.transform.localScale = Vector3.one * 2f;
            var shader = Shader.Find("MizoTake/Virtual Light/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);
            receiver.GetComponent<Renderer>().sharedMaterial = material;
            var lightObject = new GameObject("Standard Lighting Option Test Directional Light");
            var standardLight = lightObject.AddComponent<Light>();
            standardLight.type = LightType.Directional;
            standardLight.color = Color.white;
            standardLight.intensity = 2f;
            material.SetFloat("_ReceiveStandardLighting", 0f);
            material.EnableKeyword("_RECEIVE_STANDARD_LIGHTING_OFF");
            yield return null;
            var disabled = RenderCenter(camera, renderTexture);
            material.SetFloat("_ReceiveStandardLighting", 1f);
            material.DisableKeyword("_RECEIVE_STANDARD_LIGHTING_OFF");
            yield return null;
            var enabled = RenderCenter(camera, renderTexture);

            Assert.That(enabled.r, Is.GreaterThan(disabled.r + 0.05f));
            Assert.That(enabled.g, Is.GreaterThan(disabled.g + 0.05f));
            Assert.That(enabled.b, Is.GreaterThan(disabled.b + 0.05f));
            Object.Destroy(lightObject);
            Object.Destroy(receiver);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator CameraRender_RectangleAreaUsesForwardAndTwoSidedEmission()
        {
            VirtualLightSystem.ResetForTests();
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            var cameraObject = new GameObject("Rectangle Area Direction Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -4f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            var renderTexture = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Quad);
            receiver.transform.position = Vector3.zero;
            receiver.transform.localScale = Vector3.one * 2f;
            var shader = Shader.Find("MizoTake/Virtual Light/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);
            receiver.GetComponent<Renderer>().sharedMaterial = material;
            yield return null;
            var baseline = RenderCenter(camera, renderTexture);
            var lightObject = new GameObject("Rectangle Area Direction Test Light");
            lightObject.transform.position = new Vector3(0f, 0f, -2f);
            lightObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.RectangleArea;
            virtualLight.Color = Color.red;
            virtualLight.Intensity = 30f;
            virtualLight.Range = 5f;
            virtualLight.AreaSize = Vector2.one;
            virtualLight.AreaSampleCount = 4;
            virtualLight.TwoSided = false;
            yield return null;
            var forwardFacing = RenderCenter(camera, renderTexture);
            lightObject.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            yield return null;
            var backFacing = RenderCenter(camera, renderTexture);
            virtualLight.TwoSided = true;
            yield return null;
            var twoSided = RenderCenter(camera, renderTexture);

            Assert.That(forwardFacing.r, Is.GreaterThan(baseline.r + 0.05f));
            Assert.That(backFacing.r, Is.LessThan(baseline.r + 0.02f));
            Assert.That(twoSided.r, Is.GreaterThan(baseline.r + 0.05f));
            Assert.That(twoSided.r, Is.EqualTo(forwardFacing.r).Within(0.03f));
            Object.Destroy(lightObject);
            Object.Destroy(receiver);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamOcclusion_StopsAtMarkedColliderAndRestoresRange()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Beam Occlusion Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 5f;
            virtualLight.CastShadow = true;
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.ProbeRadius = 0f;
            beamOcclusion.SurfaceOffset = 0f;
            beamOcclusion.RequireOccluderMarker = true;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Marked Beam Blocker";
            blocker.transform.position = new Vector3(0f, 0f, 2f);
            blocker.transform.localScale = new Vector3(1f, 1f, 0.2f);
            blocker.AddComponent<VirtualLightOccluder>();
            Physics.SyncTransforms();
            beamOcclusion.RefreshNow();
            yield return null;

            Assert.That(beamOcclusion.IsBlocked, Is.True);
            Assert.That(beamOcclusion.CurrentVisibleDistance, Is.EqualTo(1.9f).Within(0.02f));
            Assert.That(VirtualLightSystem.TryGetDescriptor(virtualLight.Handle, out var blockedDescriptor), Is.True);
            Assert.That(blockedDescriptor.OcclusionDistance, Is.EqualTo(beamOcclusion.CurrentVisibleDistance).Within(0.001f));
            blocker.transform.position = new Vector3(2f, 0f, 2f);
            Physics.SyncTransforms();
            beamOcclusion.RefreshNow();
            yield return null;

            Assert.That(beamOcclusion.IsBlocked, Is.False);
            Assert.That(beamOcclusion.CurrentVisibleDistance, Is.EqualTo(5f).Within(0.001f));
            Object.Destroy(blocker);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamOcclusion_DefaultModeKeepsFullBeamBoundsWhileHitAndImpactRemain()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Non-Truncated Beam Occlusion Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 5f;
            virtualLight.InnerAngle = 3f;
            virtualLight.OuterAngle = 10f;
            var beamVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beamVisual.name = "Full Range Beam Bounds";
            beamVisual.transform.SetParent(lightObject.transform, false);
            Object.Destroy(beamVisual.GetComponent<Collider>());
            var impactVisual = new GameObject("Non-Truncated Beam Impact");
            impactVisual.transform.SetParent(lightObject.transform, false);
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.BeamVisual = beamVisual.transform;
            beamOcclusion.ImpactVisual = impactVisual.transform;
            beamOcclusion.ProbeRadius = 0f;
            beamOcclusion.SurfaceOffset = 0f;
            beamOcclusion.RequireOccluderMarker = true;
            Assert.That(beamOcclusion.TruncateVisualAtFirstHit, Is.False);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Marked Centerline Beam Blocker";
            blocker.transform.position = new Vector3(0f, 0f, 2f);
            blocker.transform.localScale = new Vector3(1f, 1f, 0.2f);
            blocker.AddComponent<VirtualLightOccluder>();
            Physics.SyncTransforms();
            beamOcclusion.RefreshNow();
            yield return null;

            const float expectedHitDistance = 1.9f;
            var expectedFullRangeRadius = VirtualLightMath.EvaluateBeamRadius(virtualLight.Range, virtualLight.OuterAngle);
            Assert.That(beamOcclusion.IsBlocked, Is.True);
            Assert.That(beamOcclusion.CurrentVisibleDistance, Is.EqualTo(expectedHitDistance).Within(0.02f));
            Assert.That(Vector3.Distance(beamVisual.transform.localPosition, new Vector3(0f, 0f, virtualLight.Range * 0.5f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(beamVisual.transform.localScale, new Vector3(expectedFullRangeRadius * 2f, expectedFullRangeRadius * 2f, virtualLight.Range)), Is.LessThan(0.0001f));
            Assert.That(impactVisual.activeSelf, Is.True);
            Assert.That(Vector3.Distance(impactVisual.transform.position, new Vector3(0f, 0f, expectedHitDistance)), Is.LessThan(0.02f));
            Object.Destroy(blocker);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamOcclusion_TruncateModeCutsBeamBoundsAtMarkedHit()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Truncated Beam Occlusion Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 5f;
            virtualLight.InnerAngle = 3f;
            virtualLight.OuterAngle = 10f;
            var beamVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beamVisual.name = "Truncated Beam Bounds";
            beamVisual.transform.SetParent(lightObject.transform, false);
            Object.Destroy(beamVisual.GetComponent<Collider>());
            var impactVisual = new GameObject("Truncated Beam Impact");
            impactVisual.transform.SetParent(lightObject.transform, false);
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.BeamVisual = beamVisual.transform;
            beamOcclusion.ImpactVisual = impactVisual.transform;
            beamOcclusion.ProbeRadius = 0f;
            beamOcclusion.SurfaceOffset = 0f;
            beamOcclusion.RequireOccluderMarker = true;
            beamOcclusion.TruncateVisualAtFirstHit = true;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Marked Legacy Truncation Blocker";
            blocker.transform.position = new Vector3(0f, 0f, 2f);
            blocker.transform.localScale = new Vector3(1f, 1f, 0.2f);
            blocker.AddComponent<VirtualLightOccluder>();
            Physics.SyncTransforms();
            beamOcclusion.RefreshNow();
            yield return null;

            const float expectedHitDistance = 1.9f;
            var expectedTruncatedRadius = VirtualLightMath.EvaluateBeamRadius(expectedHitDistance, virtualLight.OuterAngle);
            Assert.That(beamOcclusion.IsBlocked, Is.True);
            Assert.That(beamOcclusion.CurrentVisibleDistance, Is.EqualTo(expectedHitDistance).Within(0.02f));
            Assert.That(Vector3.Distance(beamVisual.transform.localPosition, new Vector3(0f, 0f, expectedHitDistance * 0.5f)), Is.LessThan(0.02f));
            Assert.That(Vector3.Distance(beamVisual.transform.localScale, new Vector3(expectedTruncatedRadius * 2f, expectedTruncatedRadius * 2f, expectedHitDistance)), Is.LessThan(0.02f));
            Assert.That(impactVisual.activeSelf, Is.True);
            Assert.That(Vector3.Distance(impactVisual.transform.position, new Vector3(0f, 0f, expectedHitDistance)), Is.LessThan(0.02f));
            Object.Destroy(blocker);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamImpact_FaceOnSurfaceMatchesConeDiameterAndNormalOffset()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Face-On Beam Impact Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 10f;
            virtualLight.InnerAngle = 10f;
            virtualLight.OuterAngle = 20f;
            var impactVisual = new GameObject("Physical Face-On Impact");
            impactVisual.transform.SetParent(lightObject.transform, false);
            impactVisual.transform.localScale = new Vector3(0.14f, 0.14f, 0.02f);
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.ImpactVisual = impactVisual.transform;
            beamOcclusion.ProbeRadius = 0f;
            beamOcclusion.SurfaceOffset = 0.02f;
            beamOcclusion.RequireOccluderMarker = false;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Face-On Impact Receiver";
            blocker.transform.position = new Vector3(0f, 0f, 5f);
            blocker.transform.localScale = new Vector3(8f, 8f, 0.2f);
            Physics.SyncTransforms();
            beamOcclusion.RefreshNow();
            yield return null;

            const float hitDistance = 4.9f;
            var expectedDiameter = VirtualLightMath.EvaluateBeamRadius(hitDistance, virtualLight.OuterAngle) * 2f;
            Assert.That(impactVisual.activeSelf, Is.True);
            Assert.That(impactVisual.transform.localScale.x, Is.EqualTo(expectedDiameter).Within(0.001f));
            Assert.That(impactVisual.transform.localScale.y, Is.EqualTo(expectedDiameter).Within(0.001f));
            Assert.That(impactVisual.transform.localScale.z, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(impactVisual.transform.position.z, Is.EqualTo(hitDistance - beamOcclusion.SurfaceOffset).Within(0.001f));
            Assert.That(Vector3.Dot(impactVisual.transform.forward, Vector3.back), Is.GreaterThan(0.999f));
            Object.Destroy(blocker);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamImpact_ObliqueSurfaceMatchesAnalyticFootprint()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Oblique Beam Impact Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 10f;
            virtualLight.InnerAngle = 10f;
            virtualLight.OuterAngle = 20f;
            var impactVisual = new GameObject("Physical Oblique Impact");
            impactVisual.transform.SetParent(lightObject.transform, false);
            impactVisual.transform.localScale = new Vector3(0.14f, 0.14f, 0.02f);
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.ImpactVisual = impactVisual.transform;
            beamOcclusion.ProbeRadius = 0f;
            beamOcclusion.SurfaceOffset = 0.015f;
            beamOcclusion.RequireOccluderMarker = false;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Oblique Impact Receiver";
            blocker.transform.position = new Vector3(0f, 0f, 5f);
            blocker.transform.rotation = Quaternion.AngleAxis(40f, Vector3.up);
            blocker.transform.localScale = new Vector3(8f, 8f, 0.2f);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(lightObject.transform.position, lightObject.transform.forward, out var hit, virtualLight.Range), Is.True);
            Assert.That(VirtualLightMath.TryEvaluateBeamFootprint(lightObject.transform.position, lightObject.transform.forward, lightObject.transform.right, hit.point, hit.normal, virtualLight.Range, virtualLight.OuterAngle, 0f, out var expected), Is.True);
            beamOcclusion.RefreshNow();
            yield return null;

            Assert.That(impactVisual.activeSelf, Is.True);
            Assert.That(impactVisual.transform.localScale.x, Is.EqualTo(expected.Diameter.x).Within(0.001f));
            Assert.That(impactVisual.transform.localScale.y, Is.EqualTo(expected.Diameter.y).Within(0.001f));
            Assert.That(impactVisual.transform.localScale.x, Is.GreaterThan(impactVisual.transform.localScale.y));
            Assert.That(Vector3.Distance(impactVisual.transform.position, expected.Center + expected.SurfaceNormal * beamOcclusion.SurfaceOffset), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(impactVisual.transform.rotation, expected.Rotation), Is.LessThan(0.1f));
            Object.Destroy(blocker);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamVisual_FitsSpotConeAtVisibleDistance()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Beam Visual Fit Test");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 5f;
            virtualLight.InnerAngle = 3f;
            virtualLight.OuterAngle = 10f;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Raymarch Bounds";
            visual.transform.SetParent(lightObject.transform, false);
            var collider = visual.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.BeamVisual = visual.transform;
            beamOcclusion.FitVisualToSpotCone = true;
            beamOcclusion.RefreshNow();
            yield return null;

            var expectedRadius = VirtualLightMath.EvaluateBeamRadius(5f, 10f);
            Assert.That(Vector3.Distance(visual.transform.localPosition, new Vector3(0f, 0f, 2.5f)), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(visual.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(visual.transform.localScale, new Vector3(expectedRadius * 2f, expectedRadius * 2f, 5f)), Is.LessThan(0.0001f));
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamOcclusion_IgnoresColliderMarkedAsBeamVolume()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Beam Volume Ignore Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 6f;
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.ProbeRadius = 0f;
            beamOcclusion.SurfaceOffset = 0f;
            beamOcclusion.RequireOccluderMarker = false;
            var otherBeamVolume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            otherBeamVolume.name = "Other Beam Volume";
            otherBeamVolume.transform.position = new Vector3(0f, 0f, 2f);
            otherBeamVolume.transform.localScale = new Vector3(1f, 1f, 0.2f);
            otherBeamVolume.AddComponent<VirtualLightBeamVolume>();
            var physicalBlocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            physicalBlocker.name = "Physical Blocker";
            physicalBlocker.transform.position = new Vector3(0f, 0f, 4f);
            physicalBlocker.transform.localScale = new Vector3(1f, 1f, 0.2f);
            Physics.SyncTransforms();
            beamOcclusion.RefreshNow();
            yield return null;

            Assert.That(beamOcclusion.IsBlocked, Is.True);
            Assert.That(beamOcclusion.CurrentVisibleDistance, Is.EqualTo(3.9f).Within(0.02f));
            Object.Destroy(physicalBlocker);
            Object.Destroy(otherBeamVolume);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamOcclusion_SphereCastUsesSurfaceProjectionInsteadOfSweepCenterDistance()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Beam Sphere Projection Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 6f;
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.ProbeRadius = 1f;
            beamOcclusion.SurfaceOffset = 0.05f;
            beamOcclusion.RequireOccluderMarker = false;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Sphere Projection Blocker";
            blocker.transform.position = new Vector3(0f, 0f, 4.1f);
            blocker.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();
            beamOcclusion.RefreshNow();
            yield return null;

            Assert.That(beamOcclusion.IsBlocked, Is.True);
            Assert.That(beamOcclusion.CurrentVisibleDistance, Is.EqualTo(3.95f).Within(0.03f));
            Object.Destroy(blocker);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamImpact_SphereCastDoesNotInventFootprintWhenCenterRayMisses()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Off-Axis SphereCast Impact Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 6f;
            virtualLight.OuterAngle = 20f;
            var impactVisual = new GameObject("Off-Axis Impact Must Stay Hidden");
            impactVisual.transform.SetParent(lightObject.transform, false);
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.ImpactVisual = impactVisual.transform;
            beamOcclusion.ProbeRadius = 0.75f;
            beamOcclusion.SurfaceOffset = 0f;
            beamOcclusion.RequireOccluderMarker = false;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Off-Axis SphereCast Blocker";
            blocker.transform.position = new Vector3(0.9f, 0f, 3f);
            blocker.transform.localScale = new Vector3(0.4f, 1f, 0.2f);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(lightObject.transform.position, lightObject.transform.forward, virtualLight.Range), Is.False);
            beamOcclusion.RefreshNow();
            yield return null;

            Assert.That(beamOcclusion.IsBlocked, Is.True);
            Assert.That(impactVisual.activeSelf, Is.False);
            Assert.That(beamOcclusion.CurrentImpactFootprint.IsValid, Is.False);
            Object.Destroy(blocker);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator BeamOcclusion_GrowsHitBufferWhenInitialCapacityIsSaturated()
        {
            VirtualLightSystem.ResetForTests();
            var lightObject = new GameObject("Saturated Beam Hit Buffer Test Light");
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 10f;
            var beamOcclusion = lightObject.AddComponent<VirtualLightBeamOcclusion>();
            beamOcclusion.RequireOccluderMarker = false;
            beamOcclusion.ProbeRadius = 0f;
            beamOcclusion.SurfaceOffset = 0f;
            var blockerRoot = new GameObject("Forty Beam Blockers");
            for (var index = 0; index < 40; index++)
            {
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.name = "Beam Blocker " + index;
                blocker.transform.SetParent(blockerRoot.transform, false);
                blocker.transform.position = new Vector3(0f, 0f, 1f + index * 0.15f);
                blocker.transform.localScale = new Vector3(1f, 1f, 0.05f);
            }
            Physics.SyncTransforms();
            beamOcclusion.RefreshNow();
            yield return null;

            Assert.That(beamOcclusion.IsBlocked, Is.True);
            Assert.That(beamOcclusion.CurrentVisibleDistance, Is.EqualTo(0.975f).Within(0.02f));
            Assert.That(beamOcclusion.HitBufferCapacity, Is.GreaterThanOrEqualTo(64));
            Assert.That(beamOcclusion.HitBufferSaturated, Is.False);
            Object.Destroy(blockerRoot);
            Object.Destroy(lightObject);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator CameraRender_OverlappingBeamVolumesAddRadianceWithoutMutualOcclusion()
        {
            var cameraObject = new GameObject("Additive Beam Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -4f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depthTextureMode = DepthTextureMode.Depth;
            var renderTexture = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = renderTexture;
            var shader = Shader.Find("MizoTake/Virtual Light/Beam");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetFloat("_Density", 0.12f);
            material.SetFloat("_SingleScatteringAlbedo", 1f);
            material.SetFloat("_ScatteringIntensity", 2f);
            material.SetFloat("_DistanceFalloff", 0f);
            material.SetFloat("_NoiseAmount", 0f);
            material.SetFloat("_Anisotropy", 0f);
            var first = GameObject.CreatePrimitive(PrimitiveType.Cube);
            first.name = "First Additive Beam";
            first.transform.localScale = new Vector3(1.2f, 1.2f, 2f);
            first.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(first.GetComponent<Collider>());
            var second = Object.Instantiate(first);
            second.name = "Second Additive Beam";
            second.SetActive(false);
            yield return null;
            var oneBeam = RenderCenterHdr(camera, renderTexture);
            second.SetActive(true);
            yield return null;
            var twoBeams = RenderCenterHdr(camera, renderTexture);

            Assert.That(oneBeam.r, Is.GreaterThan(0.001f));
            Assert.That(twoBeams.r, Is.GreaterThan(oneBeam.r * 1.75f));
            Object.Destroy(second);
            Object.Destroy(first);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraRender_CoreRadiusWidensTheHighEnergyBeamBody()
        {
            var cameraObject = new GameObject("Beam Core Width Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(4f, 0f, 2.5f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.depthTextureMode = DepthTextureMode.Depth;
            var renderTexture = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = renderTexture;
            var shader = Shader.Find("MizoTake/Virtual Light/Beam");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Density", 0.12f);
            material.SetFloat("_SingleScatteringAlbedo", 1f);
            material.SetFloat("_ScatteringIntensity", 20f);
            material.SetFloat("_DistanceFalloff", 0f);
            material.SetFloat("_CoreStrength", 8f);
            material.SetFloat("_CoreRadius", 0.08f);
            material.SetFloat("_EdgeExponent", 4f);
            material.SetFloat("_EdgeStart", 0.98f);
            material.SetFloat("_SourceRadius", 0.08f);
            material.SetFloat("_SourceFade", 0.001f);
            material.SetFloat("_EndFade", 0.001f);
            material.SetFloat("_NoiseAmount", 0f);
            material.SetFloat("_Anisotropy", 0f);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Core Width Beam";
            beam.transform.position = new Vector3(0f, 0f, 2.5f);
            beam.transform.localScale = new Vector3(1.2f, 1.2f, 5f);
            beam.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(beam.GetComponent<Collider>());
            yield return null;
            var shoulderScreen = camera.WorldToScreenPoint(new Vector3(0f, 0.12f, 2.5f));
            var narrowCenter = RenderCenterHdr(camera, renderTexture);
            var narrowShoulder = ReadPixelHdr(renderTexture, Mathf.RoundToInt(shoulderScreen.x), Mathf.RoundToInt(shoulderScreen.y));
            material.SetFloat("_CoreRadius", 0.45f);
            var wideCenter = RenderCenterHdr(camera, renderTexture);
            var wideShoulder = ReadPixelHdr(renderTexture, Mathf.RoundToInt(shoulderScreen.x), Mathf.RoundToInt(shoulderScreen.y));

            Assert.That(narrowShoulder.r, Is.GreaterThan(0.001f));
            Assert.That(wideShoulder.r, Is.GreaterThan(narrowShoulder.r * 2f), "Increasing the optical core radius must widen the high-energy body without changing the geometric beam bounds.");
            Assert.That(wideCenter.r, Is.GreaterThan(narrowCenter.r), "A wider core must increase the integrated high-energy path through the beam center.");
            Object.Destroy(beam);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraRender_WideAngleScatterKeepsForwardMediaVisibleFromTheSide()
        {
            var cameraObject = new GameObject("Wide Angle Scatter Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(4f, 0f, 2.5f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.depthTextureMode = DepthTextureMode.Depth;
            var renderTexture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = renderTexture;
            var shader = Shader.Find("MizoTake/Virtual Light/Beam");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Density", 0.12f);
            material.SetFloat("_SingleScatteringAlbedo", 1f);
            material.SetFloat("_ScatteringIntensity", 100f);
            material.SetFloat("_DistanceFalloff", 0f);
            material.SetFloat("_CoreStrength", 1f);
            material.SetFloat("_EdgeStart", 0.8f);
            material.SetFloat("_SourceRadius", 0.08f);
            material.SetFloat("_SourceFade", 0.001f);
            material.SetFloat("_EndFade", 0.001f);
            material.SetFloat("_NoiseAmount", 0f);
            material.SetFloat("_Anisotropy", 0.45f);
            material.SetFloat("_WideAngleScatter", 0f);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Wide Angle Scatter Beam";
            beam.transform.position = new Vector3(0f, 0f, 2.5f);
            beam.transform.localScale = new Vector3(1.2f, 1.2f, 5f);
            beam.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(beam.GetComponent<Collider>());
            yield return null;
            var forwardOnly = RenderCenterHdr(camera, renderTexture);
            material.SetFloat("_WideAngleScatter", 0.5f);
            var mixedPhase = RenderCenterHdr(camera, renderTexture);

            Assert.That(forwardOnly.r, Is.GreaterThan(0.001f));
            Assert.That(mixedPhase.r, Is.GreaterThan(forwardOnly.r * 1.25f), "A normalized isotropic particle fraction must keep a forward-scattering beam readable from side views.");
            Object.Destroy(beam);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraRender_SideViewedBeamKeepsContinuousScatteringNearSource()
        {
            var cameraObject = new GameObject("Continuous Beam Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(4f, 0f, 2.5f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1.4f;
            camera.depthTextureMode = DepthTextureMode.Depth;
            var renderTexture = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = renderTexture;
            var shader = Shader.Find("MizoTake/Virtual Light/Beam");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Density", 0.2f);
            material.SetFloat("_SingleScatteringAlbedo", 1f);
            material.SetFloat("_ScatteringIntensity", 50f);
            material.SetFloat("_DistanceFalloff", 0f);
            material.SetFloat("_CoreStrength", 1f);
            material.SetFloat("_EdgeStart", 0.8f);
            material.SetFloat("_SourceFade", 0.001f);
            material.SetFloat("_EndFade", 0.001f);
            material.SetFloat("_NoiseAmount", 0f);
            material.SetFloat("_Anisotropy", 0f);
            material.SetFloat("_SourceRadius", 0.08f);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Side Viewed Continuous Beam";
            beam.transform.position = new Vector3(0f, 0f, 2.5f);
            beam.transform.localScale = new Vector3(1.2f, 1.2f, 5f);
            beam.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(beam.GetComponent<Collider>());
            yield return null;
            camera.Render();
            foreach (var axialDistance in new[] { 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.75f })
            {
                var screen = camera.WorldToScreenPoint(new Vector3(0f, 0f, axialDistance));
                var sample = ReadPixelHdr(renderTexture, Mathf.RoundToInt(screen.x), Mathf.RoundToInt(screen.y));
                Assert.That(sample.r, Is.GreaterThan(0.002f), $"A finite-aperture beam must remain continuous near its source; the sample at {axialDistance:0.00} m was dark.");
            }
            Object.Destroy(beam);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraRender_PerspectiveThinBeamAtDistantObjectSpaceOriginDoesNotDisappear()
        {
            var cameraObject = new GameObject("Thin Beam Stability Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(6f, 0f, 2.5f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 1f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.depthTextureMode = DepthTextureMode.Depth;
            var renderTexture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = renderTexture;
            var shader = Shader.Find("MizoTake/Virtual Light/Beam");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Density", 1f);
            material.SetFloat("_SingleScatteringAlbedo", 1f);
            material.SetFloat("_ScatteringIntensity", 2000f);
            material.SetFloat("_DistanceFalloff", 0f);
            material.SetFloat("_CoreStrength", 1f);
            material.SetFloat("_SourceFade", 0.001f);
            material.SetFloat("_EndFade", 0.001f);
            material.SetFloat("_NoiseAmount", 0f);
            material.SetFloat("_Anisotropy", 0f);
            material.SetFloat("_SourceRadius", 0.001f);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Numerically Thin Beam";
            beam.transform.position = new Vector3(0f, 0f, 2.5f);
            beam.transform.localScale = new Vector3(0.02f, 0.02f, 5f);
            beam.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(beam.GetComponent<Collider>());
            yield return null;

            var color = RenderCenterHdr(camera, renderTexture);

            Assert.That(float.IsFinite(color.r), Is.True);
            Assert.That(color.r, Is.GreaterThan(0.002f), "A thin beam must keep a stable ray/frustum interval when the camera is hundreds of object-space units away.");
            Object.Destroy(beam);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraRender_FiniteApertureShadowCoversBeamOutsidePointCone()
        {
            VirtualLightSystem.ResetForTests();
            var cameraObject = new GameObject("Finite Aperture Shadow Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(4f, 0f, 2.5f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 0.8f;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.cullingMask &= ~(1 << 8);
            var renderTexture = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = renderTexture;
            var lightObject = new GameObject("Finite Aperture Shadow Spot");
            lightObject.transform.rotation = Quaternion.identity;
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Range = 5f;
            virtualLight.InnerAngle = 6f;
            virtualLight.OuterAngle = 10f;
            virtualLight.CastShadow = false;
            var shader = Shader.Find("MizoTake/Virtual Light/Beam");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Density", 0.2f);
            material.SetFloat("_SingleScatteringAlbedo", 1f);
            material.SetFloat("_ScatteringIntensity", 100f);
            material.SetFloat("_DistanceFalloff", 0f);
            material.SetFloat("_CoreStrength", 1f);
            material.SetFloat("_EdgeStart", 0.98f);
            material.SetFloat("_SourceFade", 0.001f);
            material.SetFloat("_EndFade", 0.001f);
            material.SetFloat("_NoiseAmount", 0f);
            material.SetFloat("_Anisotropy", 0f);
            material.SetFloat("_SourceRadius", 0.08f);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Finite Aperture Beam Volume";
            beam.transform.SetParent(lightObject.transform, false);
            beam.transform.localPosition = new Vector3(0f, 0f, 2.5f);
            var beamRadius = VirtualLightMath.EvaluateBeamRadius(virtualLight.Range, virtualLight.OuterAngle);
            beam.transform.localScale = new Vector3(beamRadius * 2f, beamRadius * 2f, virtualLight.Range);
            beam.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(beam.GetComponent<Collider>());
            beam.AddComponent<VirtualLightBeamVolume>();
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Full Finite Aperture Blocker";
            blocker.layer = 8;
            blocker.transform.position = new Vector3(0f, 0f, 1f);
            blocker.transform.localScale = new Vector3(1f, 1f, 0.1f);
            blocker.AddComponent<VirtualLightOccluder>();
            Physics.SyncTransforms();
            yield return null;
            var sampleWorld = new Vector3(0f, 0.28f, 3f);
            var sampleScreen = camera.WorldToScreenPoint(sampleWorld);
            var unshadowed = RenderPixel(camera, renderTexture, Mathf.RoundToInt(sampleScreen.x), Mathf.RoundToInt(sampleScreen.y));
            virtualLight.CastShadow = true;
            yield return null;
            var shadowed = RenderPixel(camera, renderTexture, Mathf.RoundToInt(sampleScreen.x), Mathf.RoundToInt(sampleScreen.y));

            Assert.That(unshadowed.r, Is.GreaterThan(0.002f));
            Assert.That(shadowed.r, Is.LessThan(unshadowed.r * 0.15f), "The expanded finite-aperture region must use the same shadow projection instead of becoming unshadowed outside the point-cone FOV.");
            Object.Destroy(blocker);
            Object.Destroy(beam);
            Object.Destroy(lightObject);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator CameraRender_LegacyOcclusionDistanceDoesNotPlaneCutReceiver()
        {
            VirtualLightSystem.ResetForTests();
            var cameraObject = new GameObject("Virtual Light Spot Occlusion Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -4f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            var renderTexture = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Quad);
            receiver.transform.position = Vector3.zero;
            receiver.transform.localScale = Vector3.one * 2f;
            var material = new Material(Shader.Find("MizoTake/Virtual Light/Lit"));
            material.SetColor("_BaseColor", Color.white);
            receiver.GetComponent<Renderer>().sharedMaterial = material;
            yield return null;
            var baseline = RenderCenter(camera, renderTexture);
            var lightObject = new GameObject("Virtual Light Render Test Spot");
            lightObject.transform.position = new Vector3(0f, 0f, -2f);
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Color = Color.red;
            virtualLight.Intensity = 30f;
            virtualLight.Range = 5f;
            virtualLight.InnerAngle = 45f;
            virtualLight.OuterAngle = 60f;
            virtualLight.CastShadow = true;
            virtualLight.OcclusionDistance = 5f;
            yield return null;
            var unblocked = RenderCenter(camera, renderTexture);
            virtualLight.OcclusionDistance = 1f;
            yield return null;
            var blocked = RenderCenter(camera, renderTexture);

            Assert.That(unblocked.r, Is.GreaterThan(baseline.r + 0.05f));
            Assert.That(blocked.r - baseline.r, Is.EqualTo(unblocked.r - baseline.r).Within(0.02f));
            Object.Destroy(lightObject);
            Object.Destroy(receiver);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        [UnityTest]
        public IEnumerator CameraRender_OffAxisOccluderShadowsOnlyItsProjectedRegion()
        {
            VirtualLightSystem.ResetForTests();
            var cameraObject = new GameObject("Virtual Light Local Shadow Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 1.2f;
            camera.cullingMask &= ~(1 << 8);
            var renderTexture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = renderTexture;
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Quad);
            receiver.transform.position = Vector3.zero;
            receiver.transform.localScale = Vector3.one * 2f;
            var material = new Material(Shader.Find("MizoTake/Virtual Light/Lit"));
            material.SetColor("_BaseColor", Color.white);
            receiver.GetComponent<Renderer>().sharedMaterial = material;
            var lightObject = new GameObject("Virtual Light Local Shadow Spot");
            lightObject.transform.position = new Vector3(0f, 0f, -2f);
            var virtualLight = lightObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            virtualLight.Type = VirtualLightType.Spot;
            virtualLight.Color = Color.red;
            virtualLight.Intensity = 5f;
            virtualLight.Range = 5f;
            virtualLight.InnerAngle = 45f;
            virtualLight.OuterAngle = 60f;
            virtualLight.CastShadow = false;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Off Axis Virtual Light Blocker";
            blocker.layer = 8;
            blocker.transform.position = new Vector3(-0.35f, 0.3f, -1f);
            blocker.transform.localScale = new Vector3(0.35f, 0.35f, 0.2f);
            blocker.AddComponent<VirtualLightOccluder>();
            Physics.SyncTransforms();
            yield return null;
            var unshadowedTarget = RenderPixel(camera, renderTexture, 27, 96);
            var unshadowedControl = RenderPixel(camera, renderTexture, 83, 96);
            var unshadowedMirrored = RenderPixel(camera, renderTexture, 27, 32);
            virtualLight.CastShadow = true;
            yield return null;
            var shadowedTarget = RenderPixel(camera, renderTexture, 27, 96);
            var shadowedControl = RenderPixel(camera, renderTexture, 83, 96);
            var shadowedMirrored = RenderPixel(camera, renderTexture, 27, 32);

            Assert.That(unshadowedTarget.r, Is.GreaterThan(0.05f));
            Assert.That(shadowedTarget.r, Is.LessThan(unshadowedTarget.r * 0.5f));
            Assert.That(shadowedControl.r, Is.GreaterThan(unshadowedControl.r * 0.75f));
            Assert.That(shadowedMirrored.r, Is.GreaterThan(unshadowedMirrored.r * 0.75f));
            Object.Destroy(blocker);
            Object.Destroy(lightObject);
            Object.Destroy(receiver);
            Object.Destroy(cameraObject);
            Object.Destroy(material);
            renderTexture.Release();
            Object.Destroy(renderTexture);
            yield return null;
            VirtualLightSystem.ResetForTests();
        }

        private static Color RenderCenter(Camera camera, RenderTexture renderTexture)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            texture.ReadPixels(new Rect(renderTexture.width / 2, renderTexture.height / 2, 1, 1), 0, 0);
            texture.Apply();
            var color = texture.GetPixel(0, 0);
            Object.DestroyImmediate(texture);
            RenderTexture.active = previous;
            return color;
        }

        private static Color RenderCenterHdr(Camera camera, RenderTexture renderTexture)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            texture.ReadPixels(new Rect(renderTexture.width / 2, renderTexture.height / 2, 1, 1), 0, 0);
            texture.Apply();
            var color = texture.GetPixel(0, 0);
            Object.DestroyImmediate(texture);
            RenderTexture.active = previous;
            return color;
        }

        private static Color RenderPixel(Camera camera, RenderTexture renderTexture, int x, int y)
        {
            camera.Render();
            return ReadPixelHdr(renderTexture, x, y);
        }

        private static Color ReadPixelHdr(RenderTexture renderTexture, int x, int y)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            texture.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            texture.Apply();
            var color = texture.GetPixel(0, 0);
            Object.DestroyImmediate(texture);
            RenderTexture.active = previous;
            return color;
        }
    }
}
