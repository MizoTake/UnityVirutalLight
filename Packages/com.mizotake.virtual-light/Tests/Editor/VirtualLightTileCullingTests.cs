using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MizoTake.VirtualLight.Tests
{
    public sealed class VirtualLightTileCullingTests
    {
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(31, 1)]
        [TestCase(32, 1)]
        [TestCase(33, 2)]
        [TestCase(64, 2)]
        [TestCase(65, 3)]
        public void TileMaskWordCount_PacksThirtyTwoLightsPerUint(int lightCount, int expectedWordCount)
        {
            Assert.That(VirtualLightRenderBridge.CalculateTileMaskWordCount(lightCount), Is.EqualTo(expectedWordCount));
        }

        [TestCase(1, 1, 1)]
        [TestCase(4, 32, 4)]
        [TestCase(4, 33, 8)]
        [TestCase(256, 65, 768)]
        public void TileMaskElementCount_IsTileCountTimesMaskWordCount(int tileCount, int lightCount, int expectedElementCount)
        {
            Assert.That(VirtualLightRenderBridge.CalculateTileMaskElementCount(tileCount, lightCount), Is.EqualTo(expectedElementCount));
        }

        [Test]
        public void ComputeShader_UsesOneGroupOfSixtyFourThreadsPerTileAndAtomicBitMasks()
        {
            var computeShader = Resources.Load<ComputeShader>("VirtualLightTileCulling");
            Assert.That(computeShader, Is.Not.Null);
            var source = File.ReadAllText(AssetDatabase.GetAssetPath(computeShader));

            StringAssert.Contains("[numthreads(64, 1, 1)]", source);
            StringAssert.Contains("SV_GroupID", source);
            StringAssert.Contains("SV_GroupThreadID", source);
            StringAssert.Contains("InterlockedOr", source);
            StringAssert.Contains("lightIndex >> 5u", source);
            StringAssert.Contains("lightIndex & 31u", source);
            StringAssert.DoesNotContain("= lightIndex;", source);
        }

        [Test]
        public void ComputeShader_DirectionalLightsSetExpectedBitsAcrossWordBoundaryForEveryTile()
        {
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("Compute shaders are unavailable on this graphics device.");
            var shaderAsset = Resources.Load<ComputeShader>("VirtualLightTileCulling");
            Assert.That(shaderAsset, Is.Not.Null);
            var computeShader = Object.Instantiate(shaderAsset);
            var lights = new VirtualLightGpu[65];
            var descriptor = VirtualLightDescriptor.Default;
            descriptor.Type = VirtualLightType.Directional;
            for (var lightIndex = 0; lightIndex < lights.Length; lightIndex++) lights[lightIndex] = VirtualLightGpu.FromDescriptor(in descriptor);
            var lightBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, lights.Length, Marshal.SizeOf<VirtualLightGpu>());
            var countBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(uint));
            var maskBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 6, sizeof(uint));
            try
            {
                var kernel = computeShader.FindKernel("CullTiles");
                lightBuffer.SetData(lights);
                computeShader.SetInt("_VirtualLightCount", lights.Length);
                computeShader.SetInt("_VirtualLightTileStride", 3);
                computeShader.SetVector("_VirtualLightScreenSize", new Vector4(32f, 16f, 2f, 1f));
                computeShader.SetFloat("_VirtualLightProjectionScale", 1f);
                computeShader.SetMatrix("_VirtualLightView", Matrix4x4.identity);
                computeShader.SetMatrix("_VirtualLightViewProjection", Matrix4x4.identity);
                computeShader.SetBuffer(kernel, "_VirtualLights", lightBuffer);
                computeShader.SetBuffer(kernel, "_VirtualLightTileCounts", countBuffer);
                computeShader.SetBuffer(kernel, "_VirtualLightTileIndices", maskBuffer);
                computeShader.Dispatch(kernel, 2, 1, 1);
                var counts = new uint[2];
                var masks = new uint[6];
                countBuffer.GetData(counts);
                maskBuffer.GetData(masks);

                Assert.That(counts, Is.EqualTo(new uint[] { 65u, 65u }));
                Assert.That(masks, Is.EqualTo(new uint[] { uint.MaxValue, uint.MaxValue, 1u, uint.MaxValue, uint.MaxValue, 1u }));
            }
            finally
            {
                lightBuffer.Dispose();
                countBuffer.Dispose();
                maskBuffer.Dispose();
                Object.DestroyImmediate(computeShader);
            }
        }

        [Test]
        public void ReceiverShader_EnumeratesSetBitsAndGuardsThePartialLastWord()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VirtualLightTileCullingTests).Assembly);
            Assert.That(packageInfo, Is.Not.Null);
            var source = File.ReadAllText(Path.Combine(packageInfo.resolvedPath, "Runtime", "Shaders", "VirtualLight.hlsl"));

            StringAssert.Contains("uint maskWordCount = (uint)_VirtualLightTileParams.w;", source);
            StringAssert.Contains("firstbitlow(activeMask)", source);
            StringAssert.Contains("activeMask &= activeMask - 1u;", source);
            StringAssert.Contains("if (lightIndex < _VirtualLightCount)", source);
        }
    }
}
