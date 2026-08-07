using MizoTake.VirtualLight;
using UnityEngine;

namespace MizoTake.VirtualLight.Samples
{
    public sealed class VirtualLightBeamDemoController : MonoBehaviour
    {
        [SerializeField] private Transform movingOccluder;
        [SerializeField] private VirtualLightBeamOcclusion beamOcclusion;
        [SerializeField] private VirtualLightSampleOverlay overlay;
        [SerializeField] private Vector3 clearPosition;
        [SerializeField] private Vector3 blockedPosition;
        [SerializeField, Min(1f)] private float cycleDuration = 7f;
        private Rigidbody movingOccluderBody;

        private void Awake()
        {
            if (movingOccluder != null) movingOccluderBody = movingOccluder.GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (movingOccluder == null) return;
            var phase = Mathf.Repeat(Time.fixedTime, cycleDuration);
            var position = Vector3.Lerp(clearPosition, blockedPosition, EvaluateBlockedBlend(phase));
            if (movingOccluderBody != null) movingOccluderBody.MovePosition(position);
            else movingOccluder.position = position;
        }

        private void Update()
        {
            if (overlay == null || beamOcclusion == null) return;
            overlay.BeamStatus = beamOcclusion.IsBlocked ? "SHADOWED  -  FIRST HIT STOPS BEAM" : "CLEAR  -  FULL BEAM TO TARGET";
            overlay.BeamStatusColor = beamOcclusion.IsBlocked ? new Color(1f, 0.48f, 0.24f) : new Color(0.12f, 0.82f, 1f);
        }

        private float EvaluateBlockedBlend(float phase)
        {
            var scale = cycleDuration / 7f;
            if (phase < 1.5f * scale) return 0f;
            if (phase < 2.5f * scale) return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f * scale, 2.5f * scale, phase));
            if (phase < 4.5f * scale) return 1f;
            if (phase < 5.5f * scale) return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(4.5f * scale, 5.5f * scale, phase));
            return 0f;
        }
    }
}
