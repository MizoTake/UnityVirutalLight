using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MizoTake.VirtualLight.Editor
{
    public static class VirtualLightLitMaterialConverter
    {
        public const string SourceShaderName = "Universal Render Pipeline/Lit";
        public const string TargetShaderName = "MizoTake/Virtual Light/Lit";
        private const string MenuPath = "Tools/Virtual Light/Convert URP Lit Materials in Loaded Scenes";

        [MenuItem(MenuPath)]
        private static void ConvertLoadedSceneMaterials()
        {
            var materials = FindConvertibleMaterialsInLoadedScenes();
            if (materials.Count == 0)
            {
                EditorUtility.DisplayDialog("Virtual Light Lit Converter", "No Universal Render Pipeline/Lit materials were found on Renderers in the loaded scenes.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Virtual Light Lit Converter", $"Convert {materials.Count} shared material(s) used by Renderers in the loaded scenes to {TargetShaderName}?\n\nMaterial assets are converted in place, so other scenes and prefabs that reference the same assets are also affected. The operation can be undone in the current Editor session.", "Convert", "Cancel")) return;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convert URP Lit Materials to Virtual Light Lit");
            var convertedCount = 0;
            foreach (var material in materials)
            {
                if (!ConvertMaterial(material)) continue;
                convertedCount++;
                if (EditorUtility.IsPersistent(material)) AssetDatabase.SaveAssetIfDirty(material);
            }
            Undo.CollapseUndoOperations(undoGroup);
            MarkScenesContainingNonPersistentMaterialsDirty(materials);
            var skippedCount = materials.Count - convertedCount;
            var skippedMessage = skippedCount == 0 ? string.Empty : $"\n\nSkipped {skippedCount} material(s) that cannot be edited in place. See the Console for details.";
            EditorUtility.DisplayDialog("Virtual Light Lit Converter", $"Converted {convertedCount} of {materials.Count} material(s).{skippedMessage}", "OK");
        }

        public static bool CanConvert(Material material)
        {
            return material != null && material.shader != null && string.Equals(material.shader.name, SourceShaderName, StringComparison.Ordinal);
        }

        public static bool ConvertMaterial(Material material, bool recordUndo = true)
        {
            if (!CanConvert(material)) return false;
            if (!CanConvertInPlace(material, out var reason))
            {
                Debug.LogWarning($"Virtual Light Lit conversion skipped '{material.name}': {reason}", material);
                return false;
            }
            var targetShader = Shader.Find(TargetShaderName);
            if (targetShader == null)
            {
                Debug.LogError($"Virtual Light Lit conversion failed because shader '{TargetShaderName}' was not found.", material);
                return false;
            }
            var snapshot = MaterialSnapshot.Capture(material, targetShader);
            if (recordUndo) Undo.RecordObject(material, "Convert URP Lit Material to Virtual Light Lit");
            material.shader = targetShader;
            snapshot.Restore(material);
            EditorUtility.SetDirty(material);
            return true;
        }

        public static bool CanConvertInPlace(Material material, out string reason)
        {
            reason = string.Empty;
            if (material == null)
            {
                reason = "The material is null.";
                return false;
            }
            if (!EditorUtility.IsPersistent(material)) return true;
            var assetPath = AssetDatabase.GetAssetPath(material);
            if (AssetDatabase.IsSubAsset(material))
            {
                reason = $"The material is embedded in '{assetPath}'. Extract or duplicate it as a writable .mat asset before conversion.";
                return false;
            }
            var serializedMaterial = new SerializedObject(material);
            var parentProperty = serializedMaterial.FindProperty("m_Parent");
            if (parentProperty != null && parentProperty.objectReferenceValue != null)
            {
                reason = "Material Variants cannot change shader independently from their parent.";
                return false;
            }
            if (!AssetDatabase.IsOpenForEdit(material, StatusQueryOptions.UseCachedIfPossible))
            {
                reason = $"The asset '{assetPath}' is read-only or not checked out.";
                return false;
            }
            return true;
        }

        public static IReadOnlyList<Material> FindConvertibleMaterialsInLoadedScenes()
        {
            var materials = new HashSet<Material>();
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var material in renderer.sharedMaterials)
                        {
                            if (CanConvert(material)) materials.Add(material);
                        }
                    }
                }
            }
            return materials.OrderBy(material => AssetDatabase.GetAssetPath(material), StringComparer.Ordinal).ThenBy(material => material.name, StringComparer.Ordinal).ThenBy(material => material.GetInstanceID()).ToArray();
        }

        private static void MarkScenesContainingNonPersistentMaterialsDirty(IReadOnlyCollection<Material> convertedMaterials)
        {
            var nonPersistentMaterials = new HashSet<Material>(convertedMaterials.Where(material => !EditorUtility.IsPersistent(material)));
            if (nonPersistentMaterials.Count == 0) return;
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;
                var containsConvertedMaterial = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).SelectMany(renderer => renderer.sharedMaterials).Any(nonPersistentMaterials.Contains);
                if (containsConvertedMaterial) EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private sealed class MaterialSnapshot
        {
            private readonly List<PropertySnapshot> properties;
            private readonly string[] keywordNames;
            private readonly int renderQueue;
            private readonly bool enableInstancing;
            private readonly bool doubleSidedGi;
            private readonly MaterialGlobalIlluminationFlags globalIlluminationFlags;

            private MaterialSnapshot(List<PropertySnapshot> properties, string[] keywordNames, int renderQueue, bool enableInstancing, bool doubleSidedGi, MaterialGlobalIlluminationFlags globalIlluminationFlags)
            {
                this.properties = properties;
                this.keywordNames = keywordNames;
                this.renderQueue = renderQueue;
                this.enableInstancing = enableInstancing;
                this.doubleSidedGi = doubleSidedGi;
                this.globalIlluminationFlags = globalIlluminationFlags;
            }

            public static MaterialSnapshot Capture(Material material, Shader targetShader)
            {
                var targetPropertyTypes = new Dictionary<string, ShaderPropertyType>(StringComparer.Ordinal);
                for (var propertyIndex = 0; propertyIndex < targetShader.GetPropertyCount(); propertyIndex++) targetPropertyTypes[targetShader.GetPropertyName(propertyIndex)] = targetShader.GetPropertyType(propertyIndex);
                var properties = new List<PropertySnapshot>();
                var sourceShader = material.shader;
                for (var propertyIndex = 0; propertyIndex < sourceShader.GetPropertyCount(); propertyIndex++)
                {
                    var propertyName = sourceShader.GetPropertyName(propertyIndex);
                    var propertyType = sourceShader.GetPropertyType(propertyIndex);
                    if (targetPropertyTypes.TryGetValue(propertyName, out var targetPropertyType) && targetPropertyType == propertyType) properties.Add(PropertySnapshot.Capture(material, propertyName, propertyType));
                }
                return new MaterialSnapshot(properties, material.enabledKeywords.Select(keyword => keyword.name).ToArray(), material.renderQueue, material.enableInstancing, material.doubleSidedGI, material.globalIlluminationFlags);
            }

            public void Restore(Material material)
            {
                foreach (var property in properties) property.Restore(material);
                foreach (var keywordName in keywordNames)
                {
                    var keyword = material.shader.keywordSpace.FindKeyword(keywordName);
                    if (keyword.isValid) material.SetKeyword(keyword, true);
                }
                material.renderQueue = renderQueue;
                material.enableInstancing = enableInstancing;
                material.doubleSidedGI = doubleSidedGi;
                material.globalIlluminationFlags = globalIlluminationFlags;
            }
        }

        private sealed class PropertySnapshot
        {
            private readonly string propertyName;
            private readonly ShaderPropertyType propertyType;
            private readonly object value;
            private readonly Vector2 textureScale;
            private readonly Vector2 textureOffset;

            private PropertySnapshot(string propertyName, ShaderPropertyType propertyType, object value, Vector2 textureScale, Vector2 textureOffset)
            {
                this.propertyName = propertyName;
                this.propertyType = propertyType;
                this.value = value;
                this.textureScale = textureScale;
                this.textureOffset = textureOffset;
            }

            public static PropertySnapshot Capture(Material material, string propertyName, ShaderPropertyType propertyType)
            {
                switch (propertyType)
                {
                    case ShaderPropertyType.Color: return new PropertySnapshot(propertyName, propertyType, material.GetColor(propertyName), default, default);
                    case ShaderPropertyType.Vector: return new PropertySnapshot(propertyName, propertyType, material.GetVector(propertyName), default, default);
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: return new PropertySnapshot(propertyName, propertyType, material.GetFloat(propertyName), default, default);
                    case ShaderPropertyType.Int: return new PropertySnapshot(propertyName, propertyType, material.GetInteger(propertyName), default, default);
                    case ShaderPropertyType.Texture: return new PropertySnapshot(propertyName, propertyType, material.GetTexture(propertyName), material.GetTextureScale(propertyName), material.GetTextureOffset(propertyName));
                    default: throw new ArgumentOutOfRangeException(nameof(propertyType), propertyType, null);
                }
            }

            public void Restore(Material material)
            {
                switch (propertyType)
                {
                    case ShaderPropertyType.Color: material.SetColor(propertyName, (Color)value); break;
                    case ShaderPropertyType.Vector: material.SetVector(propertyName, (Vector4)value); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: material.SetFloat(propertyName, (float)value); break;
                    case ShaderPropertyType.Int: material.SetInteger(propertyName, (int)value); break;
                    case ShaderPropertyType.Texture:
                        material.SetTexture(propertyName, (Texture)value);
                        material.SetTextureScale(propertyName, textureScale);
                        material.SetTextureOffset(propertyName, textureOffset);
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
