using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MizoTake.VirtualLight.Editor
{
    public sealed class VirtualLightLitShaderGUI : BaseShaderGUI
    {
        public const string ReceiveStandardLightingProperty = "_ReceiveStandardLighting";
        public const string ReceiveStandardLightingOffKeyword = "_RECEIVE_STANDARD_LIGHTING_OFF";
        private static readonly string[] WorkflowModeNames = Enum.GetNames(typeof(LitGUI.WorkflowMode));
        private static readonly GUIContent ReceiveStandardLightingLabel = EditorGUIUtility.TrTextContent("Receive Standard Lighting", "Receive URP main and additional lights, their shadows, baked lighting, reflection probes, and ambient lighting in addition to Virtual Lights.");
        private static readonly GUIContent DetailInputsLabel = EditorGUIUtility.TrTextContent("Detail Inputs", "These settings define the surface details by tiling and overlaying additional maps on the material.");
        private static readonly GUIContent DetailMaskLabel = EditorGUIUtility.TrTextContent("Mask", "The alpha channel masks the detail maps.");
        private static readonly GUIContent DetailAlbedoLabel = EditorGUIUtility.TrTextContent("Base Map", "The alpha channel controls the detail hue and intensity.");
        private static readonly GUIContent DetailNormalLabel = EditorGUIUtility.TrTextContent("Normal Map", "Adds small-scale normal detail to the material.");
        private LitGUI.LitProperties litProperties;
        private MaterialProperty receiveStandardLighting;
        private MaterialProperty detailMask;
        private MaterialProperty detailAlbedoMapScale;
        private MaterialProperty detailAlbedoMap;
        private MaterialProperty detailNormalMapScale;
        private MaterialProperty detailNormalMap;

        public override void FillAdditionalFoldouts(MaterialHeaderScopeList materialScopesList)
        {
            materialScopesList.RegisterHeaderScope(DetailInputsLabel, Expandable.Details, _ => DrawDetailInputs());
        }

        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            litProperties = new LitGUI.LitProperties(properties);
            receiveStandardLighting = FindProperty(ReceiveStandardLightingProperty, properties);
            detailMask = FindProperty("_DetailMask", properties, false);
            detailAlbedoMapScale = FindProperty("_DetailAlbedoMapScale", properties, false);
            detailAlbedoMap = FindProperty("_DetailAlbedoMap", properties, false);
            detailNormalMapScale = FindProperty("_DetailNormalMapScale", properties, false);
            detailNormalMap = FindProperty("_DetailNormalMap", properties, false);
        }

        public override void ValidateMaterial(Material material)
        {
            SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, SetVirtualLightKeywords);
        }

        public override void DrawSurfaceOptions(Material material)
        {
            EditorGUIUtility.labelWidth = 0f;
            if (litProperties.workflowMode != null) DoPopup(LitGUI.Styles.workflowModeText, litProperties.workflowMode, WorkflowModeNames);
            base.DrawSurfaceOptions(material);
            materialEditor.ShaderProperty(receiveStandardLighting, ReceiveStandardLightingLabel);
        }

        public override void DrawSurfaceInputs(Material material)
        {
            base.DrawSurfaceInputs(material);
            LitGUI.Inputs(litProperties, materialEditor, material);
            DrawEmissionProperties(material, true);
            DrawTileOffset(materialEditor, baseMapProp);
        }

        public override void DrawAdvancedOptions(Material material)
        {
            if (litProperties.reflections != null && litProperties.highlights != null)
            {
                materialEditor.ShaderProperty(litProperties.highlights, LitGUI.Styles.highlightsText);
                materialEditor.ShaderProperty(litProperties.reflections, LitGUI.Styles.reflectionsText);
            }
            base.DrawAdvancedOptions(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (material.HasProperty("_Emission")) material.SetColor("_EmissionColor", material.GetColor("_Emission"));
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/", StringComparison.Ordinal))
            {
                SetupMaterialBlendMode(material);
                return;
            }
            var surfaceType = SurfaceType.Opaque;
            var blendMode = BlendMode.Alpha;
            if (oldShader.name.Contains("/Transparent/Cutout/", StringComparison.Ordinal))
            {
                material.SetFloat("_AlphaClip", 1f);
            }
            else if (oldShader.name.Contains("/Transparent/", StringComparison.Ordinal))
            {
                surfaceType = SurfaceType.Transparent;
            }
            material.SetFloat("_Blend", (float)blendMode);
            material.SetFloat("_Surface", (float)surfaceType);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", surfaceType == SurfaceType.Transparent);
            if (oldShader.name.Equals("Standard (Specular setup)", StringComparison.Ordinal))
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Specular);
                var texture = material.GetTexture("_SpecGlossMap");
                if (texture != null) material.SetTexture("_MetallicSpecGlossMap", texture);
            }
            else
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Metallic);
                var texture = material.GetTexture("_MetallicGlossMap");
                if (texture != null) material.SetTexture("_MetallicSpecGlossMap", texture);
            }
        }

        private void DrawDetailInputs()
        {
            materialEditor.TexturePropertySingleLine(DetailMaskLabel, detailMask);
            materialEditor.TexturePropertySingleLine(DetailAlbedoLabel, detailAlbedoMap, detailAlbedoMap.textureValue != null ? detailAlbedoMapScale : null);
            if (detailAlbedoMapScale.floatValue != 1f) EditorGUILayout.HelpBox("A Detail Albedo scale other than 1 uses a more expensive shader variant.", MessageType.Info, true);
            if (detailAlbedoMap.textureValue is Texture2D detailAlbedoTexture && GraphicsFormatUtility.IsSRGBFormat(detailAlbedoTexture.graphicsFormat)) EditorGUILayout.HelpBox("The Detail Albedo texture must use a linear texture format.", MessageType.Warning, true);
            materialEditor.TexturePropertySingleLine(DetailNormalLabel, detailNormalMap, detailNormalMap.textureValue != null ? detailNormalMapScale : null);
            materialEditor.TextureScaleOffsetProperty(detailAlbedoMap);
        }

        private static void SetVirtualLightKeywords(Material material)
        {
            if (material.HasProperty("_DetailAlbedoMap") && material.HasProperty("_DetailNormalMap") && material.HasProperty("_DetailAlbedoMapScale"))
            {
                var isScaled = material.GetFloat("_DetailAlbedoMapScale") != 1f;
                var hasDetailMap = material.GetTexture("_DetailAlbedoMap") != null || material.GetTexture("_DetailNormalMap") != null;
                SetKeyword(material, "_DETAIL_MULX2", !isScaled && hasDetailMap);
                SetKeyword(material, "_DETAIL_SCALED", isScaled && hasDetailMap);
            }
            SetKeyword(material, ReceiveStandardLightingOffKeyword, material.HasProperty(ReceiveStandardLightingProperty) && material.GetFloat(ReceiveStandardLightingProperty) == 0f);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }
    }
}
