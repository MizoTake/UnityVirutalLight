using System.Linq;
using NUnit.Framework;

namespace MizoTake.VirtualLight.PerformanceBenchmark.Tests
{
    public sealed class VirtualLightBenchmarkScenarioTests
    {
        [Test]
        public void DefaultMatrixContainsPairedUnshadowedAndShadowedScenarios()
        {
            var scenarios = VirtualLightBenchmarkScenarios.CreateDefault();

            Assert.That(scenarios.Count, Is.EqualTo(18));
            Assert.That(scenarios.Count(value => !value.shadows && value.lightingMode == BenchmarkLightingMode.Standard), Is.EqualTo(5));
            Assert.That(scenarios.Count(value => !value.shadows && value.lightingMode == BenchmarkLightingMode.Virtual), Is.EqualTo(5));
            Assert.That(scenarios.Count(value => value.shadows && value.lightingMode == BenchmarkLightingMode.Standard), Is.EqualTo(4));
            Assert.That(scenarios.Count(value => value.shadows && value.lightingMode == BenchmarkLightingMode.Virtual), Is.EqualTo(4));
        }

        [Test]
        public void ShadowedScenariosNeverExceedComparableAtlasCapacity()
        {
            var scenarios = VirtualLightBenchmarkScenarios.CreateDefault();

            Assert.That(VirtualLightBenchmarkScenarios.StandardShadowAtlasResolution / VirtualLightBenchmarkScenarios.ShadowResolution, Is.EqualTo(4));
            Assert.That(scenarios.Where(value => value.shadows).Max(value => value.lightCount), Is.EqualTo(VirtualLightBenchmarkScenarios.MaximumComparableShadowedSpotCount));
            Assert.That(scenarios.Where(value => value.shadows).All(value => value.lightCount <= 16), Is.True);
        }

        [Test]
        public void StatisticsCalculateMedianP95AndMean()
        {
            var statistics = BenchmarkStatistics.Calculate(new long[] { 100, 2, 4, 1, 3 }, 5, 1d);

            Assert.That(statistics.Median, Is.EqualTo(3d));
            Assert.That(statistics.P95, Is.EqualTo(100d));
            Assert.That(statistics.Mean, Is.EqualTo(22d));
            Assert.That(statistics.SampleCount, Is.EqualTo(5));
        }

        [Test]
        public void CommandLineOptionsSupportEqualsAndSeparateValues()
        {
            var options = VirtualLightBenchmarkRunOptions.Parse(new[] { "player.exe", "--benchmark-auto", "--benchmark-only=shadowed", "--benchmark-warmup", "60", "--benchmark-samples=180", "--benchmark-repeats=2", "--benchmark-output", "D:\\Benchmark", "--benchmark-quit" });

            Assert.That(options.AutoRun, Is.True);
            Assert.That(options.QuitWhenComplete, Is.True);
            Assert.That(options.Filter, Is.EqualTo("shadowed"));
            Assert.That(options.WarmupFrames, Is.EqualTo(60));
            Assert.That(options.SampleFrames, Is.EqualTo(180));
            Assert.That(options.Repeats, Is.EqualTo(2));
            Assert.That(options.OutputDirectory, Is.EqualTo("D:\\Benchmark"));
        }
    }
}
