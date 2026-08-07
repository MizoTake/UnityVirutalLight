using UnityEditor;
using UnityEngine;

namespace MizoTake.VirtualLight.Editor
{
    [CustomEditor(typeof(MizoTake.VirtualLight.VirtualLight)), CanEditMultipleObjects]
    public sealed class VirtualLightEditor : UnityEditor.Editor
    {
        private SerializedProperty type;
        private SerializedProperty shape;
        private SerializedProperty color;
        private SerializedProperty intensity;
        private SerializedProperty range;
        private SerializedProperty innerAngle;
        private SerializedProperty outerAngle;
        private SerializedProperty spotPenumbraSharpness;
        private SerializedProperty areaSize;
        private SerializedProperty areaSampleCount;
        private SerializedProperty twoSided;
        private SerializedProperty castShadow;
        private SerializedProperty affectOpaque;
        private SerializedProperty goboTexture;
        private SerializedProperty staticPriority;
        private SerializedProperty priority;
        private SerializedProperty alwaysShowGizmo;
        private SerializedProperty showInfluenceVolume;
        private SerializedProperty showSamplePoints;

        private void OnEnable()
        {
            type = serializedObject.FindProperty("type");
            shape = serializedObject.FindProperty("shape");
            color = serializedObject.FindProperty("color");
            intensity = serializedObject.FindProperty("intensity");
            range = serializedObject.FindProperty("range");
            innerAngle = serializedObject.FindProperty("innerAngle");
            outerAngle = serializedObject.FindProperty("outerAngle");
            spotPenumbraSharpness = serializedObject.FindProperty("spotPenumbraSharpness");
            areaSize = serializedObject.FindProperty("areaSize");
            areaSampleCount = serializedObject.FindProperty("areaSampleCount");
            twoSided = serializedObject.FindProperty("twoSided");
            castShadow = serializedObject.FindProperty("castShadow");
            affectOpaque = serializedObject.FindProperty("affectOpaque");
            goboTexture = serializedObject.FindProperty("goboTexture");
            staticPriority = serializedObject.FindProperty("staticPriority");
            priority = serializedObject.FindProperty("priority");
            alwaysShowGizmo = serializedObject.FindProperty("alwaysShowGizmo");
            showInfluenceVolume = serializedObject.FindProperty("showInfluenceVolume");
            showSamplePoints = serializedObject.FindProperty("showSamplePoints");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(type);
            if (type.hasMultipleDifferentValues || VirtualLightMath.SupportsShape((VirtualLightType)type.intValue)) EditorGUILayout.PropertyField(shape);
            EditorGUILayout.PropertyField(color);
            EditorGUILayout.PropertyField(intensity);
            EditorGUILayout.PropertyField(goboTexture, new GUIContent("Gobo / Cookie"));
            if (type.hasMultipleDifferentValues || (VirtualLightType)type.intValue != VirtualLightType.Directional) EditorGUILayout.PropertyField(range);
            else if (castShadow.boolValue || goboTexture.objectReferenceValue != null) EditorGUILayout.PropertyField(range, new GUIContent("Shadow / Gobo Coverage"));
            if (!type.hasMultipleDifferentValues && (VirtualLightType)type.intValue == VirtualLightType.Spot)
            {
                var minimumAngle = innerAngle.floatValue;
                var maximumAngle = outerAngle.floatValue;
                EditorGUILayout.MinMaxSlider(new GUIContent("Cone Angles"), ref minimumAngle, ref maximumAngle, 0f, 179f);
                innerAngle.floatValue = minimumAngle;
                outerAngle.floatValue = maximumAngle;
                EditorGUILayout.PropertyField(innerAngle);
                EditorGUILayout.PropertyField(outerAngle);
                EditorGUILayout.PropertyField(spotPenumbraSharpness, new GUIContent("Surface Penumbra Sharpness"));
            }
            if (!type.hasMultipleDifferentValues && (VirtualLightType)type.intValue == VirtualLightType.RectangleArea)
            {
                EditorGUILayout.PropertyField(areaSize);
                EditorGUILayout.IntPopup(areaSampleCount, new GUIContent[] { new GUIContent("1"), new GUIContent("2"), new GUIContent("4"), new GUIContent("8"), new GUIContent("16") }, new[] { 1, 2, 4, 8, 16 });
                EditorGUILayout.PropertyField(twoSided);
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(castShadow);
            EditorGUILayout.PropertyField(affectOpaque);
            EditorGUILayout.PropertyField(staticPriority, new GUIContent("Pinned Priority"));
            EditorGUILayout.PropertyField(priority);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(alwaysShowGizmo);
            if (type.hasMultipleDifferentValues || (VirtualLightType)type.intValue != VirtualLightType.Directional) EditorGUILayout.PropertyField(showInfluenceVolume);
            if (!type.hasMultipleDifferentValues && (VirtualLightType)type.intValue == VirtualLightType.RectangleArea) EditorGUILayout.PropertyField(showSamplePoints);
            serializedObject.ApplyModifiedProperties();
            DrawWarnings();
        }

        private void OnSceneGUI()
        {
            var virtualLight = (MizoTake.VirtualLight.VirtualLight)target;
            var transform = virtualLight.transform;
            if (virtualLight.Type != VirtualLightType.Directional)
            {
                EditorGUI.BeginChangeCheck();
                var newRange = Handles.RadiusHandle(Quaternion.identity, transform.position, virtualLight.Range);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(virtualLight, "Change Virtual Light Range");
                    virtualLight.Range = newRange;
                    EditorUtility.SetDirty(virtualLight);
                }
            }
            if (virtualLight.Type == VirtualLightType.RectangleArea) DrawAreaHandle(virtualLight);
            if (virtualLight.Type == VirtualLightType.Spot) DrawSpotHandle(virtualLight);
        }

        private static void DrawAreaHandle(MizoTake.VirtualLight.VirtualLight virtualLight)
        {
            var transform = virtualLight.transform;
            var halfSize = virtualLight.AreaSize * 0.5f;
            EditorGUI.BeginChangeCheck();
            var rightPosition = Handles.Slider(transform.position + transform.right * halfSize.x, transform.right, HandleUtility.GetHandleSize(transform.position) * 0.08f, Handles.CubeHandleCap, 0f);
            var upPosition = Handles.Slider(transform.position + transform.up * halfSize.y, transform.up, HandleUtility.GetHandleSize(transform.position) * 0.08f, Handles.CubeHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(virtualLight, "Resize Virtual Area Light");
            virtualLight.AreaSize = new Vector2(Mathf.Max(0.01f, Vector3.Dot(rightPosition - transform.position, transform.right) * 2f), Mathf.Max(0.01f, Vector3.Dot(upPosition - transform.position, transform.up) * 2f));
            EditorUtility.SetDirty(virtualLight);
        }

        private static void DrawSpotHandle(MizoTake.VirtualLight.VirtualLight virtualLight)
        {
            var transform = virtualLight.transform;
            var distance = virtualLight.Range;
            var outerRadius = Mathf.Tan(virtualLight.OuterAngle * Mathf.Deg2Rad * 0.5f) * distance;
            var handlePosition = transform.position + transform.forward * distance + transform.right * outerRadius;
            EditorGUI.BeginChangeCheck();
            var changedPosition = Handles.Slider(handlePosition, transform.right, HandleUtility.GetHandleSize(handlePosition) * 0.08f, Handles.CubeHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(virtualLight, "Change Virtual Spot Angle");
            var radius = Mathf.Max(0f, Vector3.Dot(changedPosition - (transform.position + transform.forward * distance), transform.right));
            virtualLight.OuterAngle = Mathf.Clamp(Mathf.Atan2(radius, Mathf.Max(distance, 0.01f)) * 2f * Mathf.Rad2Deg, virtualLight.InnerAngle, 179f);
            EditorUtility.SetDirty(virtualLight);
        }

        private void DrawWarnings()
        {
            foreach (var selectedTarget in targets)
            {
                var virtualLight = (MizoTake.VirtualLight.VirtualLight)selectedTarget;
                var scale = virtualLight.transform.lossyScale;
                if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
                {
                    EditorGUILayout.HelpBox($"{virtualLight.name}: Negative Transform scale is not supported. Range and area dimensions are explicit values.", MessageType.Warning);
                }
            }
            var selectedLight = (MizoTake.VirtualLight.VirtualLight)target;
            if (selectedLight.CastShadow) EditorGUILayout.HelpBox("Custom shadow maps are generated from active Virtual Light Occluder hierarchies. Point uses six slices, Spot uses one, Rectangle Area uses front/back center projections, and Directional uses one camera-centered non-cascaded projection sized by Range.", MessageType.Info);
        }
    }
}
