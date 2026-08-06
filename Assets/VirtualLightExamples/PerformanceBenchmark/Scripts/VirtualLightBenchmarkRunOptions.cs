using System;

namespace MizoTake.VirtualLight.PerformanceBenchmark
{
    public sealed class VirtualLightBenchmarkRunOptions
    {
        public bool AutoRun { get; private set; }
        public bool QuitWhenComplete { get; private set; }
        public int WarmupFrames { get; private set; } = 120;
        public int SampleFrames { get; private set; } = 300;
        public int Repeats { get; private set; } = 1;
        public string Filter { get; private set; } = "all";
        public string OutputDirectory { get; private set; }

        public static VirtualLightBenchmarkRunOptions Parse(string[] arguments)
        {
            var options = new VirtualLightBenchmarkRunOptions();
            if (arguments == null) return options;
            for (var index = 0; index < arguments.Length; index++)
            {
                var argument = arguments[index];
                if (argument == "--benchmark-auto") options.AutoRun = true;
                else if (argument == "--benchmark-quit") options.QuitWhenComplete = true;
                else if (TryReadValue(arguments, ref index, "--benchmark-warmup", out var warmup)) options.WarmupFrames = ParsePositive(warmup, 120);
                else if (TryReadValue(arguments, ref index, "--benchmark-samples", out var samples)) options.SampleFrames = ParsePositive(samples, 300);
                else if (TryReadValue(arguments, ref index, "--benchmark-repeats", out var repeats)) options.Repeats = ParsePositive(repeats, 1);
                else if (TryReadValue(arguments, ref index, "--benchmark-only", out var filter)) options.Filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter;
                else if (TryReadValue(arguments, ref index, "--benchmark-output", out var output)) options.OutputDirectory = string.IsNullOrWhiteSpace(output) ? null : output;
            }
            return options;
        }

        private static bool TryReadValue(string[] arguments, ref int index, string optionName, out string value)
        {
            var argument = arguments[index];
            var prefix = optionName + "=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = argument.Substring(prefix.Length);
                return true;
            }
            if (argument == optionName && index + 1 < arguments.Length)
            {
                value = arguments[++index];
                return true;
            }
            value = null;
            return false;
        }

        private static int ParsePositive(string value, int fallback)
        {
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }
    }
}
