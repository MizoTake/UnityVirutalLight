using UnityEngine;

namespace MizoTake.VirtualLight.Samples
{
    public sealed class VirtualLightDemoController : MonoBehaviour
    {
        [SerializeField] private MizoTake.VirtualLight.VirtualLight animatedPointLight;
        [SerializeField] private MizoTake.VirtualLight.VirtualLight animatedSpotLight;
        [SerializeField] private MizoTake.VirtualLight.VirtualLight animatedAreaLight;
        [SerializeField] private Transform spotTarget;
        [SerializeField] private float orbitRadius = 0.8f;
        [SerializeField] private float orbitSpeed = 0.55f;
        [SerializeField] private Vector3 orbitCenter = new Vector3(-2.25f, 1.45f, 0.55f);
        [SerializeField] private float pointVerticalMotion = 0.22f;
        [SerializeField] private float areaPulseAmount = 0.8f;
        private float initialAreaIntensity;

        private void Awake()
        {
            initialAreaIntensity = animatedAreaLight != null ? animatedAreaLight.Intensity : 0f;
        }

        private void Update()
        {
            var phase = Time.time * orbitSpeed;
            if (animatedPointLight != null)
            {
                animatedPointLight.transform.position = orbitCenter + new Vector3(Mathf.Cos(phase) * orbitRadius, 0.75f + Mathf.Sin(phase * 1.7f) * pointVerticalMotion, Mathf.Sin(phase) * orbitRadius);
            }
            if (animatedSpotLight != null && spotTarget != null)
            {
                var targetOffset = Vector3.up * (Mathf.Sin(phase * 0.65f) * 0.2f);
                animatedSpotLight.transform.rotation = Quaternion.LookRotation(spotTarget.position + targetOffset - animatedSpotLight.transform.position, Vector3.up);
            }
            if (animatedAreaLight != null)
            {
                animatedAreaLight.Intensity = initialAreaIntensity + Mathf.Sin(phase * 0.8f) * areaPulseAmount;
            }
        }
    }
}
