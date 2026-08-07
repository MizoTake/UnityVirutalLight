using UnityEditor;
using UnityEngine;

namespace MizoTake.VirtualLight.Editor
{
    internal static class VirtualLightGizmos
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active)]
        private static void Draw(MizoTake.VirtualLight.VirtualLight virtualLight, GizmoType gizmoType)
        {
            var selected = (gizmoType & GizmoType.Selected) != 0;
            if (!selected && !virtualLight.AlwaysShowGizmo) return;
            var color = virtualLight.isActiveAndEnabled ? NormalizeColor(virtualLight.Color, selected ? 0.9f : 0.35f) : new Color(0.5f, 0.5f, 0.5f, selected ? 0.8f : 0.3f);
            Handles.color = color;
            Gizmos.color = color;
            var transform = virtualLight.transform;
            var iconSize = HandleUtility.GetHandleSize(transform.position) * 0.15f;
            var shapeLabel = VirtualLightMath.SupportsShape(virtualLight.Type) ? $" {virtualLight.Shape}" : string.Empty;
            Handles.Label(transform.position + Vector3.up * iconSize, $"VL {virtualLight.Type}{shapeLabel}");
            switch (virtualLight.Type)
            {
                case VirtualLightType.Point:
                    DrawPoint(virtualLight, iconSize);
                    break;
                case VirtualLightType.Spot:
                    DrawSpot(virtualLight, iconSize);
                    break;
                case VirtualLightType.RectangleArea:
                    DrawArea(virtualLight, iconSize);
                    break;
                case VirtualLightType.Directional:
                    DrawDirectional(virtualLight, iconSize);
                    break;
            }
        }

        private static void DrawPoint(MizoTake.VirtualLight.VirtualLight virtualLight, float iconSize)
        {
            var transform = virtualLight.transform;
            var position = transform.position;
            foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back }) Handles.DrawLine(position + direction * iconSize * 0.3f, position + direction * iconSize);
            if (!virtualLight.ShowInfluenceVolume) return;
            if (virtualLight.Shape == VirtualLightShape.Rectangle)
            {
                DrawBox(position, transform.right, transform.up, transform.forward, virtualLight.Range);
                return;
            }
            Handles.DrawWireDisc(position, Vector3.up, virtualLight.Range);
            Handles.DrawWireDisc(position, Vector3.right, virtualLight.Range);
            Handles.DrawWireDisc(position, Vector3.forward, virtualLight.Range);
        }

        private static void DrawSpot(MizoTake.VirtualLight.VirtualLight virtualLight, float iconSize)
        {
            var transform = virtualLight.transform;
            Handles.ArrowHandleCap(0, transform.position, transform.rotation, iconSize, EventType.Repaint);
            if (!virtualLight.ShowInfluenceVolume) return;
            if (virtualLight.Shape == VirtualLightShape.Rectangle)
            {
                DrawPyramid(transform.position, transform.forward, transform.right, transform.up, virtualLight.Range, virtualLight.OuterAngle);
                DrawPyramid(transform.position, transform.forward, transform.right, transform.up, virtualLight.Range, virtualLight.InnerAngle);
                return;
            }
            DrawCone(transform.position, transform.forward, transform.right, transform.up, virtualLight.Range, virtualLight.OuterAngle);
            DrawCone(transform.position, transform.forward, transform.right, transform.up, virtualLight.Range, virtualLight.InnerAngle);
        }

        private static void DrawArea(MizoTake.VirtualLight.VirtualLight virtualLight, float iconSize)
        {
            var transform = virtualLight.transform;
            var halfWidth = transform.right * virtualLight.AreaSize.x * 0.5f;
            var halfHeight = transform.up * virtualLight.AreaSize.y * 0.5f;
            var corners = new[] { transform.position - halfWidth - halfHeight, transform.position + halfWidth - halfHeight, transform.position + halfWidth + halfHeight, transform.position - halfWidth + halfHeight };
            Handles.DrawAAPolyLine(2f, corners[0], corners[1], corners[2], corners[3], corners[0]);
            Handles.ArrowHandleCap(0, transform.position, transform.rotation, iconSize, EventType.Repaint);
            if (virtualLight.TwoSided) Handles.ArrowHandleCap(0, transform.position, Quaternion.LookRotation(-transform.forward, transform.up), iconSize, EventType.Repaint);
            if (virtualLight.ShowInfluenceVolume)
            {
                foreach (var corner in corners) Handles.DrawDottedLine(corner, corner + transform.forward * virtualLight.Range, 4f);
            }
            if (virtualLight.ShowSamplePoints) DrawAreaSamples(virtualLight);
        }

        private static void DrawDirectional(MizoTake.VirtualLight.VirtualLight virtualLight, float iconSize)
        {
            var transform = virtualLight.transform;
            var arrowSize = iconSize * 1.5f;
            var offset = iconSize * 0.6f;
            Handles.ArrowHandleCap(0, transform.position, transform.rotation, arrowSize, EventType.Repaint);
            Handles.ArrowHandleCap(0, transform.position + transform.right * offset, transform.rotation, arrowSize, EventType.Repaint);
            Handles.ArrowHandleCap(0, transform.position - transform.right * offset, transform.rotation, arrowSize, EventType.Repaint);
            Handles.ArrowHandleCap(0, transform.position + transform.up * offset, transform.rotation, arrowSize, EventType.Repaint);
            Handles.ArrowHandleCap(0, transform.position - transform.up * offset, transform.rotation, arrowSize, EventType.Repaint);
        }

        private static void DrawAreaSamples(MizoTake.VirtualLight.VirtualLight virtualLight)
        {
            var count = virtualLight.AreaSampleCount;
            var grid = VirtualLightMath.GetAreaSampleGrid(count, virtualLight.AreaSize);
            var transform = virtualLight.transform;
            var size = HandleUtility.GetHandleSize(transform.position) * 0.025f;
            for (var index = 0; index < count; index++)
            {
                var x = index % grid.x;
                var y = index / grid.x;
                var unitOffset = new Vector2((x + 0.5f) / grid.x - 0.5f, (y + 0.5f) / grid.y - 0.5f);
                var position = transform.position + transform.right * unitOffset.x * virtualLight.AreaSize.x + transform.up * unitOffset.y * virtualLight.AreaSize.y;
                Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
            }
        }

        private static void DrawCone(Vector3 origin, Vector3 forward, Vector3 right, Vector3 up, float range, float angle)
        {
            var radius = Mathf.Tan(angle * Mathf.Deg2Rad * 0.5f) * range;
            var center = origin + forward * range;
            Handles.DrawWireDisc(center, forward, radius);
            Handles.DrawLine(origin, center + right * radius);
            Handles.DrawLine(origin, center - right * radius);
            Handles.DrawLine(origin, center + up * radius);
            Handles.DrawLine(origin, center - up * radius);
        }

        private static void DrawPyramid(Vector3 origin, Vector3 forward, Vector3 right, Vector3 up, float range, float angle)
        {
            var halfExtent = Mathf.Tan(angle * Mathf.Deg2Rad * 0.5f) * range;
            var center = origin + forward * range;
            var corners = new[] { center - right * halfExtent - up * halfExtent, center + right * halfExtent - up * halfExtent, center + right * halfExtent + up * halfExtent, center - right * halfExtent + up * halfExtent };
            Handles.DrawAAPolyLine(2f, corners[0], corners[1], corners[2], corners[3], corners[0]);
            foreach (var corner in corners) Handles.DrawLine(origin, corner);
        }

        private static void DrawBox(Vector3 center, Vector3 right, Vector3 up, Vector3 forward, float halfExtent)
        {
            var corners = new[] { center - right * halfExtent - up * halfExtent - forward * halfExtent, center + right * halfExtent - up * halfExtent - forward * halfExtent, center + right * halfExtent + up * halfExtent - forward * halfExtent, center - right * halfExtent + up * halfExtent - forward * halfExtent, center - right * halfExtent - up * halfExtent + forward * halfExtent, center + right * halfExtent - up * halfExtent + forward * halfExtent, center + right * halfExtent + up * halfExtent + forward * halfExtent, center - right * halfExtent + up * halfExtent + forward * halfExtent };
            Handles.DrawAAPolyLine(2f, corners[0], corners[1], corners[2], corners[3], corners[0]);
            Handles.DrawAAPolyLine(2f, corners[4], corners[5], corners[6], corners[7], corners[4]);
            for (var index = 0; index < 4; index++) Handles.DrawLine(corners[index], corners[index + 4]);
        }

        private static Color NormalizeColor(Color source, float alpha)
        {
            var maximum = Mathf.Max(source.r, Mathf.Max(source.g, source.b));
            var scale = maximum > 1f ? 1f / maximum : 1f;
            return new Color(source.r * scale, source.g * scale, source.b * scale, alpha);
        }
    }
}
