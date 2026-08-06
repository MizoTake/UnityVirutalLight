using System.IO;
using UnityEditor;
using UnityEngine;

namespace MizoTake.VirtualLight.Editor
{
    public sealed class VirtualLightSystemSettingsWindow : EditorWindow
    {
        internal const string SettingsAssetPath = "Assets/Resources/MizoTake/VirtualLight/VirtualLightSystemSettings.asset";
        private VirtualLightSystemSettings settings;
        private SerializedObject serializedSettings;

        [MenuItem("Tools/Virtual Light/Settings", priority = 200)]
        public static void Open()
        {
            var window = GetWindow<VirtualLightSystemSettingsWindow>();
            window.titleContent = new GUIContent("Virtual Light Settings");
            window.minSize = new Vector2(380f, 240f);
            window.Show();
        }

        internal static VirtualLightSystemSettings CreateSettingsAssetAtPath(string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VirtualLightSystemSettings>(assetPath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                Debug.LogError($"Virtual Light system settings cannot be created because another asset already exists at '{assetPath}'.");
                return null;
            }
            var folderPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folderPath) || folderPath != "Assets" && !folderPath.StartsWith("Assets/"))
            {
                Debug.LogError($"Virtual Light system settings must be created under Assets. Requested path: '{assetPath}'.");
                return null;
            }
            EnsureFolders(folderPath);
            var created = CreateInstance<VirtualLightSystemSettings>();
            created.ResetToDefaults();
            AssetDatabase.CreateAsset(created, assetPath);
            AssetDatabase.SaveAssetIfDirty(created);
            return created;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Virtual Light Settings");
            settings = CreateSettingsAssetAtPath(SettingsAssetPath);
            serializedSettings = settings != null ? new SerializedObject(settings) : null;
            ApplyCurrentSettings();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Virtual Light System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These project-wide defaults are included in Player builds and apply to custom Virtual Light shadow maps. Unity Quality and URP shadow settings remain separate.", MessageType.Info);
            if (settings == null || serializedSettings == null)
            {
                EditorGUILayout.HelpBox($"Could not load or create the settings asset at {SettingsAssetPath}.", MessageType.Error);
                if (GUILayout.Button("Retry")) OnEnable();
                return;
            }
            serializedSettings.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("quality"));
            EditorGUILayout.LabelField("Shadow Slice Resolution", VirtualLightSystemSettings.GetShadowMapResolution(settings.Quality).ToString());
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("shadowDepthBias"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("shadowNormalBias"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("shadowCasterLayers"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                SaveAndApply();
            }
            else
            {
                serializedSettings.ApplyModifiedProperties();
            }
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to Defaults"))
                {
                    Undo.RecordObject(settings, "Reset Virtual Light System Settings");
                    settings.ResetToDefaults();
                    serializedSettings.Update();
                    SaveAndApply();
                }
                if (GUILayout.Button("Select Settings Asset")) Selection.activeObject = settings;
            }
        }

        private void SaveAndApply()
        {
            settings.Normalize();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            ApplyCurrentSettings();
        }

        private void ApplyCurrentSettings()
        {
            if (settings == null) return;
            VirtualLightSystem.Current.ApplySettings(settings);
            SceneView.RepaintAll();
            Repaint();
        }

        private static void EnsureFolders(string folderPath)
        {
            var segments = folderPath.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }

    [InitializeOnLoad]
    internal static class VirtualLightSystemSettingsEditorLifecycle
    {
        static VirtualLightSystemSettingsEditorLifecycle()
        {
            EditorApplication.delayCall += ApplyProjectSettings;
            Undo.undoRedoPerformed += ApplyProjectSettings;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void ApplyProjectSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<VirtualLightSystemSettings>(VirtualLightSystemSettingsWindow.SettingsAssetPath);
            VirtualLightSystem.Current.ApplySettings(settings);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) ApplyProjectSettings();
        }
    }
}
