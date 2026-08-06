using System;
using System.Collections.Generic;

namespace MizoTake.VirtualLight.PerformanceBenchmark
{
    public enum BenchmarkLightingMode
    {
        Standard,
        Virtual
    }

    [Serializable]
    public sealed class BenchmarkScenario
    {
        public BenchmarkLightingMode lightingMode;
        public int lightCount;
        public bool shadows;

        public string Name => $"{lightingMode}-{(shadows ? "Shadowed" : "Unshadowed")}-{lightCount}";
    }

    public readonly struct BenchmarkStatistics
    {
        public readonly double Median;
        public readonly double P95;
        public readonly double Mean;
        public readonly int SampleCount;

        public BenchmarkStatistics(double median, double p95, double mean, int sampleCount)
        {
            Median = median;
            P95 = p95;
            Mean = mean;
            SampleCount = sampleCount;
        }

        public static BenchmarkStatistics Calculate(long[] values, int count, double divisor)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (count < 0 || count > values.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (!double.IsFinite(divisor) || divisor <= 0d) throw new ArgumentOutOfRangeException(nameof(divisor));
            if (count == 0) return new BenchmarkStatistics(0d, 0d, 0d, 0);

            var sortedValues = new long[count];
            Array.Copy(values, sortedValues, count);
            Array.Sort(sortedValues);
            var middle = count / 2;
            var median = count % 2 == 0 ? (sortedValues[middle - 1] + sortedValues[middle]) * 0.5d : sortedValues[middle];
            var p95Index = Math.Max(0, (int)Math.Ceiling(count * 0.95d) - 1);
            double sum = 0d;
            for (var index = 0; index < count; index++) sum += sortedValues[index];
            return new BenchmarkStatistics(median / divisor, sortedValues[p95Index] / divisor, sum / count / divisor, count);
        }
    }

    public static class VirtualLightBenchmarkScenarios
    {
        public const int ShadowResolution = 512;
        public const int StandardShadowAtlasResolution = 2048;
        public const int MaximumComparableShadowedSpotCount = 16;

        public static IReadOnlyList<BenchmarkScenario> CreateDefault()
        {
            var scenarios = new List<BenchmarkScenario>(18);
            AddPairs(scenarios, false, 1, 4, 16, 64, 128);
            AddPairs(scenarios, true, 1, 4, 8, MaximumComparableShadowedSpotCount);
            return scenarios;
        }

        private static void AddPairs(List<BenchmarkScenario> scenarios, bool shadows, params int[] lightCounts)
        {
            for (var index = 0; index < lightCounts.Length; index++)
            {
                scenarios.Add(new BenchmarkScenario { lightingMode = BenchmarkLightingMode.Standard, lightCount = lightCounts[index], shadows = shadows });
                scenarios.Add(new BenchmarkScenario { lightingMode = BenchmarkLightingMode.Virtual, lightCount = lightCounts[index], shadows = shadows });
            }
        }
    }
}
