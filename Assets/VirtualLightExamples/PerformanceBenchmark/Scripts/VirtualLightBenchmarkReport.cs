using System;
using System.Collections.Generic;

namespace MizoTake.VirtualLight.PerformanceBenchmark
{
    [Serializable]
    public sealed class VirtualLightBenchmarkReport
    {
        public string status;
        public string startedUtc;
        public string completedUtc;
        public string unityVersion;
        public string buildGuid;
        public string operatingSystem;
        public string graphicsDevice;
        public string graphicsApi;
        public string graphicsDriver;
        public string processor;
        public int width;
        public int height;
        public int refreshRateNumerator;
        public int refreshRateDenominator;
        public bool developmentBuild;
        public bool frameTimingStatsEnabled;
        public int standardShadowAtlasResolution;
        public int shadowResolution;
        public string equivalence;
        public string outputDirectory;
        public string error;
        public List<VirtualLightBenchmarkResult> results = new List<VirtualLightBenchmarkResult>();
    }

    [Serializable]
    public sealed class VirtualLightBenchmarkResult
    {
        public string scenario;
        public string backend;
        public int lightCount;
        public bool shadows;
        public int repetition;
        public int warmupFrames;
        public int requestedSamples;
        public int sampledFrames;
        public int cpuTotalValidSamples;
        public int cpuMainValidSamples;
        public int cpuRenderValidSamples;
        public int gpuValidSamples;
        public bool gpuTimingSupported;
        public double cpuTotalMedianMs;
        public double cpuTotalP95Ms;
        public double cpuMainMedianMs;
        public double cpuMainP95Ms;
        public double cpuRenderMedianMs;
        public double cpuRenderP95Ms;
        public double gpuMedianMs;
        public double gpuP95Ms;
        public long[] cpuTotalNanoseconds;
        public long[] cpuMainNanoseconds;
        public long[] cpuRenderNanoseconds;
        public long[] gpuNanoseconds;
        public string screenshot;
        public double screenshotMagentaPercent;
        public double screenshotNonBlackPercent;
    }
}
