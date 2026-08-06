using MizoTake.VirtualLight;
using UnityEngine;

namespace MizoTake.VirtualLight.Samples
{
    public sealed class VirtualLightArenaBeamController : MonoBehaviour
    {
        [SerializeField] private MizoTake.VirtualLight.VirtualLight[] movingBeams;
        [SerializeField] private Transform[] aimTargets;
        [SerializeField] private Vector3[] baseAimPositions;
        [SerializeField] private float[] baseIntensities;
        [SerializeField] private float[] phaseOffsets;
        [SerializeField] private VirtualLightSampleOverlay overlay;
        [SerializeField, Min(4f)] private float showDuration = 24f;
        [SerializeField, Min(0.1f)] private float motionSpeed = 0.72f;
        [SerializeField, Min(0.1f)] private float rotationDamping = 8f;
        private int lastStatusMode = -1;

        private void Update()
        {
            ApplyPreviewTime(Time.time, false);
        }

        public void ApplyPreviewTime(float time, bool immediate)
        {
            var count = movingBeams?.Length ?? 0;
            count = Mathf.Min(count, aimTargets?.Length ?? 0);
            count = Mathf.Min(count, baseAimPositions?.Length ?? 0);
            count = Mathf.Min(count, baseIntensities?.Length ?? 0);
            count = Mathf.Min(count, phaseOffsets?.Length ?? 0);
            if (count <= 0) return;
            var normalizedShowTime = Mathf.Repeat(time, showDuration) / showDuration;
            var mode = Mathf.Min(Mathf.FloorToInt(normalizedShowTime * 4f), 3);
            var modeProgress = Mathf.Repeat(normalizedShowTime * 4f, 1f);
            for (var index = 0; index < count; index++)
            {
                var beam = movingBeams[index];
                var target = aimTargets[index];
                if (beam == null || target == null) continue;
                var phase = phaseOffsets[index];
                var wave = time * motionSpeed + phase;
                var aimPosition = EvaluateAimPosition(mode, index, count, wave, modeProgress);
                target.position = aimPosition;
                var desiredRotation = Quaternion.LookRotation(aimPosition - beam.transform.position, Vector3.up);
                var interpolation = immediate ? 1f : 1f - Mathf.Exp(-rotationDamping * Time.deltaTime);
                beam.transform.rotation = Quaternion.Slerp(beam.transform.rotation, desiredRotation, interpolation);
                beam.Intensity = EvaluateIntensity(mode, index, count, modeProgress) * baseIntensities[index];
            }
            if (overlay != null && mode != lastStatusMode)
            {
                var totalOutput = 0f;
                for (var index = 0; index < count; index++) totalOutput += EvaluateIntensity(mode, index, count, modeProgress);
                overlay.Status = $"{ModeName(mode)}  /  {count} ADDITIVE BEAMS  /  PBR OUTPUT {totalOutput:0.00}x";
                lastStatusMode = mode;
            }
        }

        private Vector3 EvaluateAimPosition(int mode, int index, int count, float wave, float modeProgress)
        {
            var basePosition = baseAimPositions[index];
            if (mode == 0) return basePosition + new Vector3(Mathf.Sin(wave) * 0.9f, Mathf.Sin(wave * 0.47f) * 0.18f, Mathf.Cos(wave * 0.63f) * 0.45f);
            if (mode == 1)
            {
                var side = movingBeams[index].transform.position.x < 0f ? 1f : -1f;
                return new Vector3(side * (1.2f + Mathf.Sin(wave * 0.71f) * 0.8f), 0.65f + Mathf.Sin(wave * 0.43f) * 0.2f, 1.2f + Mathf.Cos(wave) * 0.75f);
            }
            if (mode == 2) return new Vector3(Mathf.Sin(wave + index * 0.35f) * 0.35f, 0.85f + Mathf.Cos(wave * 0.53f) * 0.18f, 1.25f + Mathf.Cos(wave + index * 0.28f) * 0.35f);
            var soloIndex = Mathf.Min(Mathf.FloorToInt(modeProgress * count), count - 1);
            return index == soloIndex ? new Vector3(Mathf.Sin(wave) * 0.75f, 0.8f, 1.2f + Mathf.Cos(wave) * 0.4f) : basePosition;
        }

        private static float EvaluateIntensity(int mode, int index, int count, float modeProgress)
        {
            if (mode == 0) return 0.62f;
            if (mode == 1) return 0.58f;
            if (mode == 2) return 0.42f;
            var soloIndex = Mathf.Min(Mathf.FloorToInt(modeProgress * count), count - 1);
            return index == soloIndex ? 1f : 0.04f;
        }

        private static string ModeName(int mode)
        {
            if (mode == 0) return "FAN SWEEP";
            if (mode == 1) return "CROSS";
            if (mode == 2) return "CONVERGE";
            return "SOLO IMPACT";
        }
    }
}
