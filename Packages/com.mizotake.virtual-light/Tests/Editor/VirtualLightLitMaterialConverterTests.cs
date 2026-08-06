using System.Linq;
using Guid = System.Guid;
using MizoTake.VirtualLight.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MizoTake.VirtualLight.Tests
{
    public sealed class VirtualLightLitMaterialConverterTests
    {
        [Test]
        public void TargetShader_DeclaresEveryUrpLitPropertyAndKeyword()
        {
            var sourceShader = Shader.Find(VirtualLightLitMaterialConverter.SourceShaderName);
            var targetShader = Shader.Find(VirtualLightLitMaterialConverter.TargetShaderName);
            Assert.That(sourceShader, Is.Not.Null);
            Assert.That(targetShader, Is.Not.Null);
            var targetProperties = Enumerable.Range(0, targetShader.GetPropertyCount()).ToDictionary(targetShader.GetPropertyName, targetShader.GetPropertyType);
            for (var propertyIndex = 0; propertyIndex < sourceShader.GetPropertyCount(); propertyIndex++)
            {
                var propertyName = sourceShader.GetPropertyName(propertyIndex);
                Assert.That(targetProperties, Does.ContainKey(propertyName), $"Target shader does not declare URP Lit property {propertyName}.");
                Assert.That(targetProperties[propertyName], Is.EqualTo(sourceShader.GetPropertyType(propertyIndex)), $"Target shader property type differs for {propertyName}.");
            }
            var targetKeywordNames = targetShader.keywordSpace.keywords.Select(keyword => keyword.name).ToArray();
            var missingKeywordNames = sourceShader.keywordSpace.keywords.Select(keyword => keyword.name).Except(targetKeywordNames).ToArray();
            Assert.That(missingKeywordNames, Is.Empty);
            var targetMaterial = new Material(targetShader);
            try
            {
                Assert.That(targetMaterial.FindPass("ForwardLit"), Is.GreaterThanOrEqualTo(0));
                Assert.That(targetMaterial.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                Assert.That(targetMaterial.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                Assert.That(targetMaterial.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
                Assert.That(targetMaterial.FindPass("DepthNormalsOnly"), Is.GreaterThanOrEqualTo(0));
                Assert.That(targetMaterial.FindPass("Meta"), Is.GreaterThanOrEqualTo(0));
                Assert.That(targetMaterial.FindPass("MotionVectors"), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(targetMaterial);
            }
        }

        [Test]
        public void ConvertMaterial_PreservesUrpLitTexturesAndParameters()
        {
            var sourceShader = Shader.Find(VirtualLightLitMaterialConverter.SourceShaderName);
            Assert.That(sourceShader, Is.Not.Null);
            var material = new Material(sourceShader);
            var baseMap = new Texture2D(2, 2);
            var metallicMap = new Texture2D(2, 2);
            var normalMap = new Texture2D(2, 2);
            var occlusionMap = new Texture2D(2, 2);
            var emissionMap = new Texture2D(2, 2);
            var detailMask = new Texture2D(2, 2);
            var detailAlbedoMap = new Texture2D(2, 2);
            var detailNormalMap = new Texture2D(2, 2);
            var parallaxMap = new Texture2D(2, 2);
            try
            {
                material.SetTexture("_BaseMap", baseMap);
                material.SetTextureScale("_BaseMap", new Vector2(2.25f, 3.5f));
                material.SetTextureOffset("_BaseMap", new Vector2(0.15f, 0.35f));
                material.SetColor("_BaseColor", new Color(0.2f, 0.4f, 0.6f, 0.8f));
                material.SetTexture("_MetallicGlossMap", metallicMap);
                material.SetFloat("_Metallic", 0.37f);
                material.SetFloat("_Smoothness", 0.73f);
                material.SetFloat("_SmoothnessTextureChannel", 1f);
                material.SetTexture("_BumpMap", normalMap);
                material.SetFloat("_BumpScale", 1.4f);
                material.SetTexture("_OcclusionMap", occlusionMap);
                material.SetFloat("_OcclusionStrength", 0.42f);
                material.SetTexture("_EmissionMap", emissionMap);
                material.SetColor("_EmissionColor", new Color(2f, 1f, 0.5f, 1f));
                material.SetTexture("_DetailMask", detailMask);
                material.SetTexture("_DetailAlbedoMap", detailAlbedoMap);
                material.SetFloat("_DetailAlbedoMapScale", 1.25f);
                material.SetTexture("_DetailNormalMap", detailNormalMap);
                material.SetFloat("_DetailNormalMapScale", 0.65f);
                material.SetTexture("_ParallaxMap", parallaxMap);
                material.SetFloat("_Parallax", 0.025f);
                material.SetFloat("_Cutoff", 0.31f);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 1f);
                material.SetFloat("_Cull", 0f);
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_SrcBlend", 1f);
                material.SetFloat("_DstBlend", 10f);
                material.SetFloat("_SrcBlendAlpha", 1f);
                material.SetFloat("_DstBlendAlpha", 10f);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
                material.EnableKeyword("_EMISSION");
                material.EnableKeyword("_DETAIL_MULX2");
                material.EnableKeyword("_PARALLAXMAP");
                material.EnableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 2471;
                material.enableInstancing = true;

                Assert.That(VirtualLightLitMaterialConverter.ConvertMaterial(material, false), Is.True);

                Assert.That(material.shader.name, Is.EqualTo(VirtualLightLitMaterialConverter.TargetShaderName));
                Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(baseMap));
                Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(2.25f, 3.5f)));
                Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(new Vector2(0.15f, 0.35f)));
                AssertColor(material.GetColor("_BaseColor"), new Color(0.2f, 0.4f, 0.6f, 0.8f));
                Assert.That(material.GetTexture("_MetallicGlossMap"), Is.SameAs(metallicMap));
                Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(0.37f));
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0.73f));
                Assert.That(material.GetFloat("_SmoothnessTextureChannel"), Is.EqualTo(1f));
                Assert.That(material.GetTexture("_BumpMap"), Is.SameAs(normalMap));
                Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(1.4f));
                Assert.That(material.GetTexture("_OcclusionMap"), Is.SameAs(occlusionMap));
                Assert.That(material.GetFloat("_OcclusionStrength"), Is.EqualTo(0.42f));
                Assert.That(material.GetTexture("_EmissionMap"), Is.SameAs(emissionMap));
                AssertColor(material.GetColor("_EmissionColor"), new Color(2f, 1f, 0.5f, 1f));
                Assert.That(material.GetTexture("_DetailMask"), Is.SameAs(detailMask));
                Assert.That(material.GetTexture("_DetailAlbedoMap"), Is.SameAs(detailAlbedoMap));
                Assert.That(material.GetFloat("_DetailAlbedoMapScale"), Is.EqualTo(1.25f));
                Assert.That(material.GetTexture("_DetailNormalMap"), Is.SameAs(detailNormalMap));
                Assert.That(material.GetFloat("_DetailNormalMapScale"), Is.EqualTo(0.65f));
                Assert.That(material.GetTexture("_ParallaxMap"), Is.SameAs(parallaxMap));
                Assert.That(material.GetFloat("_Parallax"), Is.EqualTo(0.025f));
                Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.31f));
                Assert.That(material.GetFloat("_Surface"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_Blend"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_Cull"), Is.Zero);
                Assert.That(material.GetFloat("_AlphaClip"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SrcBlend"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo(10f));
                Assert.That(material.GetFloat("_SrcBlendAlpha"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_DstBlendAlpha"), Is.EqualTo(10f));
                Assert.That(material.GetFloat("_ZWrite"), Is.Zero);
                Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Transparent"));
                Assert.That(material.GetShaderPassEnabled("ShadowCaster"), Is.False);
                Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
                Assert.That(material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True);
                Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True);
                Assert.That(material.IsKeywordEnabled("_DETAIL_MULX2"), Is.True);
                Assert.That(material.IsKeywordEnabled("_PARALLAXMAP"), Is.True);
                Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                Assert.That(material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"), Is.True);
                Assert.That(material.renderQueue, Is.EqualTo(2471));
                Assert.That(material.enableInstancing, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(baseMap);
                Object.DestroyImmediate(metallicMap);
                Object.DestroyImmediate(normalMap);
                Object.DestroyImmediate(occlusionMap);
                Object.DestroyImmediate(emissionMap);
                Object.DestroyImmediate(detailMask);
                Object.DestroyImmediate(detailAlbedoMap);
                Object.DestroyImmediate(detailNormalMap);
                Object.DestroyImmediate(parallaxMap);
            }
        }

        [Test]
        public void ConvertMaterial_PreservesSpecularWorkflowAndHiddenClearCoatValues()
        {
            var material = new Material(Shader.Find(VirtualLightLitMaterialConverter.SourceShaderName));
            var specularMap = new Texture2D(2, 2);
            try
            {
                material.SetFloat("_WorkflowMode", 0f);
                material.SetColor("_SpecColor", new Color(0.12f, 0.23f, 0.34f, 1f));
                material.SetTexture("_SpecGlossMap", specularMap);
                material.SetFloat("_ClearCoatMask", 0.68f);
                material.SetFloat("_ClearCoatSmoothness", 0.84f);
                material.EnableKeyword("_SPECULAR_SETUP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");

                Assert.That(VirtualLightLitMaterialConverter.ConvertMaterial(material, false), Is.True);

                Assert.That(material.GetFloat("_WorkflowMode"), Is.Zero);
                AssertColor(material.GetColor("_SpecColor"), new Color(0.12f, 0.23f, 0.34f, 1f));
                Assert.That(material.GetTexture("_SpecGlossMap"), Is.SameAs(specularMap));
                Assert.That(material.GetFloat("_ClearCoatMask"), Is.EqualTo(0.68f));
                Assert.That(material.GetFloat("_ClearCoatSmoothness"), Is.EqualTo(0.84f));
                Assert.That(material.GetFloat("_ClearCoat"), Is.Zero);
                Assert.That(material.IsKeywordEnabled("_SPECULAR_SETUP"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(specularMap);
            }
        }

        [Test]
        public void FindConvertibleMaterialsInLoadedScenes_DeduplicatesSharedMaterialsAndIncludesInactiveRenderers()
        {
            var sharedMaterial = new Material(Shader.Find(VirtualLightLitMaterialConverter.SourceShaderName));
            var first = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var second = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                first.GetComponent<Renderer>().sharedMaterial = sharedMaterial;
                second.GetComponent<Renderer>().sharedMaterial = sharedMaterial;
                second.SetActive(false);

                var matches = VirtualLightLitMaterialConverter.FindConvertibleMaterialsInLoadedScenes();

                Assert.That(matches.Count(material => material == sharedMaterial), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(sharedMaterial);
            }
        }

        [Test]
        public void ConvertMaterial_RejectsNonLitMaterialWithoutMutation()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(VirtualLightLitMaterialConverter.CanConvert(material), Is.False);
                Assert.That(VirtualLightLitMaterialConverter.ConvertMaterial(material, false), Is.False);
                Assert.That(material.shader, Is.SameAs(shader));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ConvertMaterial_RegistersUndo()
        {
            var sourceShader = Shader.Find(VirtualLightLitMaterialConverter.SourceShaderName);
            var material = new Material(sourceShader);
            try
            {
                Assert.That(VirtualLightLitMaterialConverter.ConvertMaterial(material), Is.True);
                Assert.That(material.shader.name, Is.EqualTo(VirtualLightLitMaterialConverter.TargetShaderName));

                Undo.PerformUndo();

                Assert.That(material.shader, Is.SameAs(sourceShader));
            }
            finally
            {
                Undo.ClearUndo(material);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void CanConvertInPlace_RejectsPersistentSubAsset()
        {
            var assetPath = $"Assets/__VirtualLightLitConverterSubAssetTest_{Guid.NewGuid():N}.shadervariants";
            var container = new ShaderVariantCollection();
            var material = new Material(Shader.Find(VirtualLightLitMaterialConverter.SourceShaderName));
            try
            {
                AssetDatabase.CreateAsset(container, assetPath);
                AssetDatabase.AddObjectToAsset(material, container);
                AssetDatabase.SaveAssets();
                Assert.That(AssetDatabase.IsSubAsset(material), Is.True);

                Assert.That(VirtualLightLitMaterialConverter.CanConvertInPlace(material, out var reason), Is.False);
                Assert.That(reason, Does.Contain("embedded"));
                Assert.That(material.shader.name, Is.EqualTo(VirtualLightLitMaterialConverter.SourceShaderName));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(Vector4.Distance(actual, expected), Is.LessThan(0.0001f));
        }
    }
}
