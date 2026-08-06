using System.Collections.Generic;
using UnityEngine;

namespace MizoTake.VirtualLight
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Virtual Light Occluder")]
    public sealed class VirtualLightOccluder : MonoBehaviour
    {
        private static readonly HashSet<VirtualLightOccluder> Active = new HashSet<VirtualLightOccluder>();
        private static readonly HashSet<Renderer> UniqueRenderers = new HashSet<Renderer>();
        private static readonly List<Renderer> ChildRenderers = new List<Renderer>(64);
        [SerializeField] private bool blocksBeam = true;

        public bool BlocksBeam { get => blocksBeam; set => blocksBeam = value; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            UniqueRenderers.Clear();
            ChildRenderers.Clear();
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        internal static void CollectShadowRenderers(List<Renderer> destination)
        {
            destination.Clear();
            UniqueRenderers.Clear();
            foreach (var occluder in Active)
            {
                if (occluder == null || !occluder.isActiveAndEnabled || !occluder.blocksBeam) continue;
                ChildRenderers.Clear();
                occluder.GetComponentsInChildren(false, ChildRenderers);
                for (var index = 0; index < ChildRenderers.Count; index++)
                {
                    var renderer = ChildRenderers[index];
                    if (renderer == null || (VirtualLightSystem.ShadowCasterLayerMask & 1 << renderer.gameObject.layer) == 0 || renderer.GetComponentInParent<VirtualLightBeamVolume>() != null || !HasOpaqueMaterial(renderer) || !UniqueRenderers.Add(renderer)) continue;
                    destination.Add(renderer);
                }
            }
        }

        private static bool HasOpaqueMaterial(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            for (var index = 0; index < materials.Length; index++)
            {
                var material = materials[index];
                if (material != null && material.renderQueue <= (int)UnityEngine.Rendering.RenderQueue.GeometryLast) return true;
            }
            return false;
        }
    }
}
