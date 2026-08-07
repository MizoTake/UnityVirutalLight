using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MizoTake.VirtualLight.Editor
{
    public readonly struct VirtualLightConversionResult
    {
        internal VirtualLightConversionResult(int requestedCount, int convertedCount, IReadOnlyList<string> skippedReasons)
        {
            RequestedCount = requestedCount;
            ConvertedCount = convertedCount;
            SkippedReasons = skippedReasons;
        }

        public int RequestedCount { get; }
        public int ConvertedCount { get; }
        public int SkippedCount => SkippedReasons.Count;
        public IReadOnlyList<string> SkippedReasons { get; }
    }

    public static class VirtualLightComponentConverter
    {
        private const string SelectedMenuPath = "Tools/Virtual Light/Convert Selected Light Components";
        private const string CurrentStageMenuPath = "Tools/Virtual Light/Convert Light Components in Current Stage";
        private const string ContextMenuPath = "CONTEXT/Light/Convert to Virtual Light";
        private const string DialogTitle = "Virtual Light Component Converter";
        private const string UndoName = "Convert Light Components to Virtual Lights";

        [MenuItem(SelectedMenuPath)]
        private static void ConvertSelectedLightComponents()
        {
            ConvertWithConfirmation(FindLightsInSelection(), "selected Light component(s)");
        }

        [MenuItem(SelectedMenuPath, true)]
        private static bool ValidateConvertSelectedLightComponents()
        {
            return CanRunMenuCommand() && FindLightsInSelection().Count > 0;
        }

        [MenuItem(CurrentStageMenuPath)]
        private static void ConvertCurrentStageLightComponents()
        {
            ConvertWithConfirmation(FindLightsInCurrentStage(), "Light component(s) in the current editing stage");
        }

        [MenuItem(CurrentStageMenuPath, true)]
        private static bool ValidateConvertCurrentStageLightComponents()
        {
            return CanRunMenuCommand();
        }

        [MenuItem(ContextMenuPath)]
        private static void ConvertContextLightComponent(MenuCommand command)
        {
            var light = command.context as Light;
            ConvertWithConfirmation(light == null ? Array.Empty<Light>() : new[] { light }, "selected Light component");
        }

        [MenuItem(ContextMenuPath, true)]
        private static bool ValidateConvertContextLightComponent(MenuCommand command)
        {
            return CanRunMenuCommand() && command.context is Light light && IsSceneObject(light);
        }

        public static IReadOnlyList<Light> FindLightsInSelection()
        {
            var lights = new HashSet<Light>(Selection.objects.OfType<Light>().Where(IsSceneObject));
            foreach (var gameObject in Selection.gameObjects)
            {
                if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) continue;
                foreach (var light in gameObject.GetComponents<Light>().Where(IsSceneObject)) lights.Add(light);
            }
            return lights.OrderBy(GetSceneSortKey, StringComparer.Ordinal).ThenBy(GetHierarchyPath, StringComparer.Ordinal).ThenBy(light => light.GetInstanceID()).ToArray();
        }

        public static IReadOnlyList<Light> FindLightsInCurrentStage()
        {
            var stage = StageUtility.GetCurrentStage();
            return stage == null ? Array.Empty<Light>() : stage.FindComponentsOfType<Light>().Where(IsSceneObject).Distinct().OrderBy(GetSceneSortKey, StringComparer.Ordinal).ThenBy(GetHierarchyPath, StringComparer.Ordinal).ThenBy(light => light.GetInstanceID()).ToArray();
        }

        public static bool CanConvert(Light light, out string reason)
        {
            if (light == null)
            {
                reason = "The Light component is null.";
                return false;
            }
            if (!IsSceneObject(light))
            {
                reason = "Only Light components in the current editing stage can be converted.";
                return false;
            }
            if ((light.hideFlags & HideFlags.NotEditable) != 0 || (light.gameObject.hideFlags & HideFlags.NotEditable) != 0 || PrefabUtility.IsPartOfImmutablePrefab(light))
            {
                reason = "The Light component is not editable.";
                return false;
            }
            if (!TryMapType(light, out _, out _))
            {
                reason = $"Light type '{light.type}' cannot be represented by Virtual Light. Supported source types are Directional, Point, cone or pyramid Spot, and Rectangle.";
                return false;
            }
            if (light.TryGetComponent<MizoTake.VirtualLight.VirtualLight>(out _))
            {
                reason = "The GameObject already has a Virtual Light component.";
                return false;
            }
            var blockingDependents = FindLightDependentComponents(light).Where(component => component is not IAdditionalData).ToArray();
            if (blockingDependents.Length > 0)
            {
                reason = $"The following component(s) require UnityEngine.Light and cannot be redirected automatically: {string.Join(", ", blockingDependents.Select(component => component.GetType().Name))}.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public static MizoTake.VirtualLight.VirtualLight ConvertLight(Light light, bool recordUndo = true)
        {
            if (!CanConvert(light, out var reason))
            {
                if (light != null) Debug.LogWarning($"Virtual Light conversion skipped '{GetHierarchyPath(light)}': {reason}", light);
                return null;
            }
            if (!recordUndo) return ConvertLightInternal(light, false);
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            var converted = ConvertLightInternal(light, true);
            Undo.CollapseUndoOperations(undoGroup);
            return converted;
        }

        public static VirtualLightConversionResult ConvertLights(IEnumerable<Light> lights, bool recordUndo = true)
        {
            var sources = (lights ?? Array.Empty<Light>()).Where(light => light != null).Distinct().ToArray();
            var skippedReasons = new List<string>();
            var convertibleSources = new List<Light>();
            foreach (var source in sources)
            {
                if (CanConvert(source, out var reason))
                {
                    convertibleSources.Add(source);
                    continue;
                }
                var message = $"'{GetHierarchyPath(source)}': {reason}";
                skippedReasons.Add(message);
                Debug.LogWarning($"Virtual Light conversion skipped {message}", source);
            }
            var undoGroup = -1;
            if (recordUndo && convertibleSources.Count > 0)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
            }
            foreach (var source in convertibleSources) ConvertLightInternal(source, recordUndo);
            if (recordUndo && undoGroup >= 0) Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
            return new VirtualLightConversionResult(sources.Length, convertibleSources.Count, skippedReasons);
        }

        private static void ConvertWithConfirmation(IReadOnlyList<Light> lights, string description)
        {
            if (lights.Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, $"No {description} were found.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog(DialogTitle, $"Replace {lights.Count} {description} with Virtual Light components?\n\nDirectional, Point, cone or pyramid Spot, and Rectangle parameters are copied when applicable. Pyramid becomes Spot + Rectangle. Box Spot and other unsupported light types are left unchanged. References whose field type is UnityEngine.Light cannot be redirected to Virtual Light and will become missing for converted components. Virtual Light receiver shaders are required, and only Spot shadows are currently supported. Scenes and Prefabs are not saved automatically. The operation can be undone in the current Editor session.", "Convert", "Cancel")) return;
            var result = ConvertLights(lights);
            var skippedMessage = result.SkippedCount == 0 ? string.Empty : $"\n\nSkipped {result.SkippedCount} component(s). See the Console for details.";
            EditorUtility.DisplayDialog(DialogTitle, $"Converted {result.ConvertedCount} of {result.RequestedCount} Light component(s).{skippedMessage}", "OK");
        }

        private static MizoTake.VirtualLight.VirtualLight ConvertLightInternal(Light source, bool recordUndo)
        {
            TryMapType(source, out var type, out var shape);
            var gameObject = source.gameObject;
            var sourceEnabled = source.enabled;
            var sourceColor = source.useColorTemperature ? source.color * Mathf.CorrelatedColorTemperatureToRGB(source.colorTemperature) : source.color;
            var additionalDataComponents = FindLightDependentComponents(source).Where(component => component is IAdditionalData).ToArray();
            var target = recordUndo ? Undo.AddComponent<MizoTake.VirtualLight.VirtualLight>(gameObject) : gameObject.AddComponent<MizoTake.VirtualLight.VirtualLight>();
            if (recordUndo) Undo.RecordObject(target, "Copy Light Parameters to Virtual Light");
            if (!sourceEnabled) target.enabled = false;
            target.Type = type;
            target.Shape = shape;
            target.Color = sourceColor;
            target.Intensity = source.intensity;
            if (type != VirtualLightType.Directional) target.Range = source.range;
            if (type == VirtualLightType.Spot)
            {
                target.OuterAngle = 179f;
                target.InnerAngle = source.innerSpotAngle;
                target.OuterAngle = source.spotAngle;
            }
            if (type == VirtualLightType.RectangleArea) target.AreaSize = source.areaSize;
            target.CastShadow = source.shadows != LightShadows.None;
            target.enabled = sourceEnabled;
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            foreach (var additionalData in additionalDataComponents) DestroyComponent(additionalData, recordUndo);
            DestroyComponent(source, recordUndo);
            EditorUtility.SetDirty(target);
            var scene = gameObject.scene;
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.MarkSceneDirty(scene);
            return target;
        }

        private static bool TryMapType(Light source, out VirtualLightType targetType, out VirtualLightShape targetShape)
        {
            targetShape = VirtualLightShape.Circle;
            switch (source.type)
            {
                case LightType.Directional:
                    targetType = VirtualLightType.Directional;
                    return true;
                case LightType.Point:
                    targetType = VirtualLightType.Point;
                    return true;
                case LightType.Spot:
                    targetType = VirtualLightType.Spot;
                    return TryMapSpotShape(source, out targetShape);
                case LightType.Pyramid:
                    targetType = VirtualLightType.Spot;
                    targetShape = VirtualLightShape.Rectangle;
                    return true;
                case LightType.Box:
                    targetType = default;
                    return false;
                case LightType.Rectangle:
                    targetType = VirtualLightType.RectangleArea;
                    return true;
                default:
                    targetType = default;
                    return false;
            }
        }

#pragma warning disable 618
        private static bool TryMapSpotShape(Light source, out VirtualLightShape targetShape)
        {
            targetShape = VirtualLightShape.Circle;
            if (source.shape == LightShape.Box) return false;
            if (source.shape == LightShape.Pyramid) targetShape = VirtualLightShape.Rectangle;
            return true;
        }
#pragma warning restore 618

        private static void DestroyComponent(Component component, bool recordUndo)
        {
            if (recordUndo) Undo.DestroyObjectImmediate(component);
            else UnityEngine.Object.DestroyImmediate(component);
        }

        private static IReadOnlyList<Component> FindLightDependentComponents(Light light)
        {
            return light.gameObject.GetComponents<Component>().Where(component => component != null && component != light && RequiresLight(component.GetType())).ToArray();
        }

        private static bool RequiresLight(Type componentType)
        {
            return componentType.GetCustomAttributes(typeof(RequireComponent), true).Cast<RequireComponent>().Any(attribute => attribute.m_Type0 == typeof(Light) || attribute.m_Type1 == typeof(Light) || attribute.m_Type2 == typeof(Light));
        }

        private static bool IsSceneObject(Light light)
        {
            return light != null && !EditorUtility.IsPersistent(light) && light.gameObject.scene.IsValid() && light.gameObject.scene.isLoaded && StageUtility.GetStageHandle(light.gameObject) == StageUtility.GetCurrentStageHandle();
        }

        private static bool CanRunMenuCommand()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !BuildPipeline.isBuildingPlayer;
        }

        private static string GetSceneSortKey(Light light)
        {
            return string.IsNullOrEmpty(light.gameObject.scene.path) ? light.gameObject.scene.name : light.gameObject.scene.path;
        }

        private static string GetHierarchyPath(Light light)
        {
            var names = new Stack<string>();
            for (var current = light.transform; current != null; current = current.parent) names.Push(current.name);
            return $"{light.gameObject.scene.name}/{string.Join("/", names)}";
        }
    }
}
