using System.Collections.Generic;
using UnityEngine;

namespace MizoTake.VirtualLight
{
    public static class VirtualLightSystem
    {
        private sealed class Entry
        {
            public VirtualLightHandle Handle;
            public VirtualLightDescriptor Descriptor;
        }

        private sealed class EntryComparer : IComparer<Entry>
        {
            public Vector3 CameraPosition;

            public int Compare(Entry left, Entry right)
            {
                var leftStatic = (left.Descriptor.Flags & VirtualLightFlags.Static) != 0;
                var rightStatic = (right.Descriptor.Flags & VirtualLightFlags.Static) != 0;
                var result = rightStatic.CompareTo(leftStatic);
                if (result != 0) return result;
                var leftShadow = (left.Descriptor.Flags & VirtualLightFlags.CastShadow) != 0;
                var rightShadow = (right.Descriptor.Flags & VirtualLightFlags.CastShadow) != 0;
                result = rightShadow.CompareTo(leftShadow);
                if (result != 0) return result;
                result = right.Descriptor.Priority.CompareTo(left.Descriptor.Priority);
                if (result != 0) return result;
                result = EstimateContribution(right.Descriptor, CameraPosition).CompareTo(EstimateContribution(left.Descriptor, CameraPosition));
                if (result != 0) return result;
                var leftGenerated = (left.Descriptor.Flags & VirtualLightFlags.Generated) != 0;
                var rightGenerated = (right.Descriptor.Flags & VirtualLightFlags.Generated) != 0;
                result = leftGenerated.CompareTo(rightGenerated);
                return result != 0 ? result : left.Handle.Id.CompareTo(right.Handle.Id);
            }

            private static float EstimateContribution(in VirtualLightDescriptor descriptor, Vector3 cameraPosition)
            {
                var distanceSquared = Mathf.Max((descriptor.Position - cameraPosition).sqrMagnitude, 0.01f);
                return descriptor.Intensity * descriptor.Radius * descriptor.Radius / distanceSquared;
            }
        }

        private sealed class Implementation : IVirtualLightSystem
        {
            private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
            private readonly Dictionary<int, uint> generations = new Dictionary<int, uint>();
            private readonly Stack<int> freeIds = new Stack<int>();
            private readonly List<Entry> selection = new List<Entry>();
            private readonly EntryComparer comparer = new EntryComparer();
            private int nextId = 1;

            public VirtualLightQuality Quality { get; private set; } = VirtualLightQuality.Medium;
            public int Count => entries.Count;

            VirtualLightHandle IVirtualLightSystem.Register(in VirtualLightDescriptor descriptor)
            {
                var id = freeIds.Count > 0 ? freeIds.Pop() : nextId++;
                generations.TryGetValue(id, out var previousGeneration);
                var generation = previousGeneration == uint.MaxValue ? 1u : previousGeneration + 1u;
                generations[id] = generation;
                var handle = new VirtualLightHandle(id, generation);
                entries.Add(id, new Entry { Handle = handle, Descriptor = descriptor.Sanitized() });
                VirtualLightRenderBridge.EnsureInitialized();
                return handle;
            }

            void IVirtualLightSystem.Update(VirtualLightHandle handle, in VirtualLightDescriptor descriptor)
            {
                if (!TryGetEntry(handle, out var entry)) return;
                var sanitized = descriptor.Sanitized();
                if (!entry.Descriptor.Equals(sanitized)) entry.Descriptor = sanitized;
            }

            void IVirtualLightSystem.Unregister(VirtualLightHandle handle)
            {
                if (!TryGetEntry(handle, out _)) return;
                entries.Remove(handle.Id);
                freeIds.Push(handle.Id);
            }

            void IVirtualLightSystem.ClearGeneratedLights()
            {
                selection.Clear();
                foreach (var entry in entries.Values)
                {
                    if ((entry.Descriptor.Flags & VirtualLightFlags.Generated) != 0) selection.Add(entry);
                }
                foreach (var entry in selection)
                {
                    entries.Remove(entry.Handle.Id);
                    freeIds.Push(entry.Handle.Id);
                }
            }

            void IVirtualLightSystem.SetQuality(VirtualLightQuality quality)
            {
                Quality = quality;
            }

            public bool TryGetDescriptor(VirtualLightHandle handle, out VirtualLightDescriptor descriptor)
            {
                if (TryGetEntry(handle, out var entry))
                {
                    descriptor = entry.Descriptor;
                    return true;
                }
                descriptor = default;
                return false;
            }

            public int FillSelected(Vector3 cameraPosition, int capacity, VirtualLightGpu[] destination, VirtualLightDescriptor[] descriptors = null, VirtualLightHandle[] handles = null)
            {
                selection.Clear();
                foreach (var entry in entries.Values)
                {
                    var descriptor = entry.Descriptor;
                    if ((descriptor.Flags & VirtualLightFlags.Enabled) != 0 && descriptor.Radius > 0f && descriptor.Intensity > 0f) selection.Add(entry);
                }
                comparer.CameraPosition = cameraPosition;
                selection.Sort(comparer);
                var count = Mathf.Min(Mathf.Min(capacity, destination.Length), selection.Count);
                for (var index = 0; index < count; index++)
                {
                    var descriptor = selection[index].Descriptor;
                    destination[index] = VirtualLightGpu.FromDescriptor(descriptor);
                    if (descriptors != null && index < descriptors.Length) descriptors[index] = descriptor;
                    if (handles != null && index < handles.Length) handles[index] = selection[index].Handle;
                }
                return count;
            }

            public void Clear()
            {
                entries.Clear();
                generations.Clear();
                freeIds.Clear();
                selection.Clear();
                nextId = 1;
                Quality = VirtualLightQuality.Medium;
            }

            private bool TryGetEntry(VirtualLightHandle handle, out Entry entry)
            {
                return entries.TryGetValue(handle.Id, out entry) && entry.Handle.Generation == handle.Generation;
            }
        }

        private static readonly Implementation Instance = new Implementation();
        public static IVirtualLightSystem Current => Instance;
        public static int RegisteredCount => Instance.Count;
        public static VirtualLightQuality Quality => Instance.Quality;

        internal static bool TryGetDescriptor(VirtualLightHandle handle, out VirtualLightDescriptor descriptor)
        {
            return Instance.TryGetDescriptor(handle, out descriptor);
        }

        internal static int FillSelected(Vector3 cameraPosition, int capacity, VirtualLightGpu[] destination)
        {
            return Instance.FillSelected(cameraPosition, capacity, destination);
        }

        internal static int FillSelected(Vector3 cameraPosition, int capacity, VirtualLightGpu[] destination, VirtualLightDescriptor[] descriptors, VirtualLightHandle[] handles)
        {
            return Instance.FillSelected(cameraPosition, capacity, destination, descriptors, handles);
        }

        internal static VirtualLightHandle[] SelectHandlesForTests(Vector3 cameraPosition, int capacity)
        {
            var selectionBuffer = new VirtualLightGpu[Mathf.Max(capacity, 0)];
            var handleBuffer = new VirtualLightHandle[selectionBuffer.Length];
            var count = Instance.FillSelected(cameraPosition, capacity, selectionBuffer, null, handleBuffer);
            var result = new VirtualLightHandle[count];
            System.Array.Copy(handleBuffer, result, count);
            return result;
        }

        internal static void ResetForTests()
        {
            Instance.Clear();
            VirtualLightRenderBridge.Dispose();
            VirtualLightRenderBridge.EnsureInitialized();
        }

        internal static void ClearForRuntimeReset()
        {
            Instance.Clear();
        }

    }
}
