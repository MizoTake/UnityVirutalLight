using UnityEngine;

namespace MizoTake.VirtualLight.Samples
{
    public sealed class VirtualLightDemoController : MonoBehaviour
    {
        [SerializeField] private MizoTake.VirtualLight.VirtualLight animatedPointLight;
        [SerializeField] private MizoTake.VirtualLight.VirtualLight animatedSpotLight;
        [SerializeField] private MizoTake.VirtualLight.VirtualLight animatedAreaLight;
        [SerializeField] private Transform spotTarget;
        [SerializeField] private VirtualLightSampleOverlay sampleOverlay;
        [SerializeField] private bool animatePunctualShape = true;
        [SerializeField, Min(0.25f)] private float shapeSwitchInterval = 4f;
        [SerializeField, Range(-180f, 180f)] private float rectangleRoll = 30f;
        [SerializeField] private float orbitRadius = 0.8f;
        [SerializeField] private float orbitSpeed = 0.55f;
        [SerializeField] private Vector3 orbitCenter = new Vector3(-2.25f, 1.45f, 0.55f);
        [SerializeField] private float pointVerticalMotion = 0.22f;
        [SerializeField] private float areaPulseAmount = 0.8f;
        private float initialAreaIntensity;
        private Quaternion initialPointRotation;
        private VirtualLightShape displayedShape = (VirtualLightShape)(-1);

        private void Awake()
        {
            initialAreaIntensity = animatedAreaLight != null ? animatedAreaLight.Intensity : 0f;
            initialPointRotation = animatedPointLight != null ? animatedPointLight.transform.rotation : Quaternion.identity;
        }

        private void Update()
        {
            var phase = Time.time * orbitSpeed;
            var punctualShape = UpdatePunctualShape(Time.time);
            if (animatedPointLight != null)
            {
                animatedPointLight.transform.position = orbitCenter + new Vector3(Mathf.Cos(phase) * orbitRadius, 0.75f + Mathf.Sin(phase * 1.7f) * pointVerticalMotion, Mathf.Sin(phase) * orbitRadius);
                animatedPointLight.transform.rotation = initialPointRotation * Quaternion.AngleAxis(punctualShape == VirtualLightShape.Rectangle ? rectangleRoll : 0f, Vector3.forward);
            }
            if (animatedSpotLight != null && spotTarget != null)
            {
                var targetOffset = Vector3.up * (Mathf.Sin(phase * 0.65f) * 0.2f);
                animatedSpotLight.transform.rotation = Quaternion.LookRotation(spotTarget.position + targetOffset - animatedSpotLight.transform.position, Vector3.up) * Quaternion.AngleAxis(punctualShape == VirtualLightShape.Rectangle ? rectangleRoll : 0f, Vector3.forward);
            }
            if (animatedAreaLight != null)
            {
                animatedAreaLight.Intensity = initialAreaIntensity + Mathf.Sin(phase * 0.8f) * areaPulseAmount;
            }
        }

        private VirtualLightShape UpdatePunctualShape(float time)
        {
            var shape = animatedPointLight != null ? animatedPointLight.Shape : animatedSpotLight != null ? animatedSpotLight.Shape : VirtualLightShape.Circle;
            if (animatePunctualShape)
            {
                shape = (Mathf.FloorToInt(Mathf.Max(0f, time) / Mathf.Max(0.25f, shapeSwitchInterval)) & 1) == 0 ? VirtualLightShape.Circle : VirtualLightShape.Rectangle;
                if (animatedPointLight != null) animatedPointLight.Shape = shape;
                if (animatedSpotLight != null) animatedSpotLight.Shape = shape;
            }
            if (sampleOverlay != null && displayedShape != shape)
            {
                displayedShape = shape;
                sampleOverlay.Status = shape == VirtualLightShape.Rectangle ? $"RUNTIME SHAPE  -  POINT + SPOT / RECTANGLE  -  ROLL {rectangleRoll:0}" : "RUNTIME SHAPE  -  POINT + SPOT / CIRCLE";
                sampleOverlay.StatusColor = shape == VirtualLightShape.Rectangle ? new Color(1f, 0.34f, 0.08f) : new Color(0.12f, 0.82f, 1f);
            }
            return shape;
        }

        private void OnValidate()
        {
            shapeSwitchInterval = Mathf.Max(0.25f, shapeSwitchInterval);
        }
    }
}
