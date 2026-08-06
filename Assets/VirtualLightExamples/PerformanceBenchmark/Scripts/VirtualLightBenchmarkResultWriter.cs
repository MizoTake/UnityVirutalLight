using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MizoTake.VirtualLight.PerformanceBenchmark
{
    public static class VirtualLightBenchmarkResultWriter
    {
        public static void Save(VirtualLightBenchmarkReport report)
        {
            Directory.CreateDirectory(report.outputDirectory);
            WriteAtomic(Path.Combine(report.outputDirectory, "results.json"), JsonUtility.ToJson(report, true));
            WriteAtomic(Path.Combine(report.outputDirectory, "summary.csv"), CreateCsv(report));
        }

        private static string CreateCsv(VirtualLightBenchmarkReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("scenario,backend,lights,shadows,repetition,cpu_total_median_ms,cpu_total_p95_ms,cpu_main_median_ms,cpu_main_p95_ms,cpu_render_median_ms,cpu_render_p95_ms,gpu_median_ms,gpu_p95_ms,gpu_valid_samples,requested_samples,sampled_frames,screenshot,magenta_percent,non_black_percent");
            for (var index = 0; index < report.results.Count; index++)
            {
                var result = report.results[index];
                Append(builder, result.scenario);
                Append(builder, result.backend);
                Append(builder, result.lightCount.ToString(CultureInfo.InvariantCulture));
                Append(builder, result.shadows ? "true" : "false");
                Append(builder, result.repetition.ToString(CultureInfo.InvariantCulture));
                Append(builder, result.cpuTotalMedianMs.ToString("F6", CultureInfo.InvariantCulture));
                Append(builder, result.cpuTotalP95Ms.ToString("F6", CultureInfo.InvariantCulture));
                Append(builder, result.cpuMainMedianMs.ToString("F6", CultureInfo.InvariantCulture));
                Append(builder, result.cpuMainP95Ms.ToString("F6", CultureInfo.InvariantCulture));
                Append(builder, result.cpuRenderMedianMs.ToString("F6", CultureInfo.InvariantCulture));
                Append(builder, result.cpuRenderP95Ms.ToString("F6", CultureInfo.InvariantCulture));
                Append(builder, result.gpuTimingSupported ? result.gpuMedianMs.ToString("F6", CultureInfo.InvariantCulture) : string.Empty);
                Append(builder, result.gpuTimingSupported ? result.gpuP95Ms.ToString("F6", CultureInfo.InvariantCulture) : string.Empty);
                Append(builder, result.gpuValidSamples.ToString(CultureInfo.InvariantCulture));
                Append(builder, result.requestedSamples.ToString(CultureInfo.InvariantCulture));
                Append(builder, result.sampledFrames.ToString(CultureInfo.InvariantCulture));
                Append(builder, result.screenshot);
                Append(builder, result.screenshotMagentaPercent.ToString("F6", CultureInfo.InvariantCulture));
                Append(builder, result.screenshotNonBlackPercent.ToString("F6", CultureInfo.InvariantCulture), true);
            }
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value, bool endOfLine = false)
        {
            builder.Append('"').Append((value ?? string.Empty).Replace("\"", "\"\"")).Append('"');
            if (endOfLine) builder.AppendLine();
            else builder.Append(',');
        }

        private static void WriteAtomic(string path, string contents)
        {
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporaryPath, path);
        }
    }
}
