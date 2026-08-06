using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace MizoTake.VirtualLight.PerformanceBenchmark
{
    [DisallowMultipleComponent]
    public sealed class VirtualLightBenchmarkController : MonoBehaviour
    {
        private enum RunState
        {
            Idle,
            Warmup,
            Sampling,
            Capturing
        }

        private readonly struct RunRequest
        {
            public RunRequest(int scenarioIndex, int repetition)
            {
                ScenarioIndex = scenarioIndex;
                Repetition = repetition;
            }

            public int ScenarioIndex { get; }
            public int Repetition { get; }
        }

        [SerializeField] private Light[] standardLights = Array.Empty<Light>();
        [SerializeField] private VirtualLight[] virtualLights = Array.Empty<VirtualLight>();
        [SerializeField] private GameObject standardReceivers;
        [SerializeField] private GameObject virtualReceivers;
        [SerializeField] private GameObject standardCasters;
        [SerializeField] private GameObject virtualCasters;
        [SerializeField] private Camera benchmarkCamera;
        private readonly Queue<RunRequest> pendingRuns = new Queue<RunRequest>();
        private IReadOnlyList<BenchmarkScenario> scenarios;
        private VirtualLightBenchmarkRunOptions options;
        private VirtualLightBenchmarkReport report;
        private RunState state;
        private RunRequest currentRequest;
        private int currentPairIndex;
        private BenchmarkLightingMode displayedMode;
        private int stateFrame;
        private int sampleIndex;
        private int captureWaitFrames;
        private long[] cpuTotalSamples;
        private long[] cpuMainSamples;
        private long[] cpuRenderSamples;
        private long[] gpuSamples;
        private int cpuTotalValidSamples;
        private int cpuMainValidSamples;
        private int cpuRenderValidSamples;
        private int gpuValidSamples;
        private ProfilerRecorder cpuTotalRecorder;
        private ProfilerRecorder cpuMainRecorder;
        private ProfilerRecorder cpuRenderRecorder;
        private ProfilerRecorder gpuRecorder;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle warningStyle;
        private bool runFailed;
        private bool virtualShaderDiagnosticsLogged;

        public Light[] StandardLights => standardLights;
        public VirtualLight[] VirtualLights => virtualLights;
        public GameObject StandardReceivers => standardReceivers;
        public GameObject VirtualReceivers => virtualReceivers;
        public GameObject StandardCasters => standardCasters;
        public GameObject VirtualCasters => virtualCasters;
        public Camera BenchmarkCamera => benchmarkCamera;

        private void Awake()
        {
            scenarios = VirtualLightBenchmarkScenarios.CreateDefault();
            options = VirtualLightBenchmarkRunOptions.Parse(Environment.GetCommandLineArgs());
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            if (benchmarkCamera != null) benchmarkCamera.targetTexture = null;
            InitializeReport();
            StartRecorders();
            ApplyScenario(0);
        }

        private void Start()
        {
            if (ContainsCommandLineArgument("-batchmode") || ContainsCommandLineArgument("-nographics"))
            {
                Fail("描画ベンチマークでは -batchmode / -nographics を使用できません。");
                return;
            }
            if (options.AutoRun) StartAllRuns();
        }

        private void OnDestroy()
        {
            cpuTotalRecorder.Dispose();
            cpuMainRecorder.Dispose();
            cpuRenderRecorder.Dispose();
            gpuRecorder.Dispose();
        }

        private void Update()
        {
            if (state == RunState.Idle)
            {
                return;
            }
            if (state == RunState.Warmup)
            {
                stateFrame++;
                if (stateFrame >= options.WarmupFrames) BeginSampling();
                return;
            }
            if (state == RunState.Sampling)
            {
                CaptureProfilerValues();
                sampleIndex++;
                var allRecordersComplete = cpuTotalValidSamples >= options.SampleFrames && cpuMainValidSamples >= options.SampleFrames && cpuRenderValidSamples >= options.SampleFrames && gpuValidSamples >= options.SampleFrames;
                if (allRecordersComplete || sampleIndex >= options.SampleFrames * 4) CompleteSampling();
                return;
            }
            captureWaitFrames++;
            if (captureWaitFrames >= 8) AdvanceRun();
        }

        private void OnGUI()
        {
            if (state == RunState.Warmup || state == RunState.Sampling) return;
            HandleManualInput(Event.current);
            EnsureStyles();
            var scale = Mathf.Max(0.65f, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
            var panel = new Rect(28f * scale, 28f * scale, 690f * scale, 370f * scale);
            var oldColor = GUI.color;
            GUI.color = new Color(0.015f, 0.02f, 0.04f, 0.92f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = oldColor;
            var scenario = scenarios[currentPairIndex * 2 + (displayedMode == BenchmarkLightingMode.Standard ? 0 : 1)];
            GUI.Label(new Rect(50f * scale, 44f * scale, 640f * scale, 34f * scale), "VIRTUAL LIGHT PERFORMANCE BENCHMARK", titleStyle);
            GUI.Label(new Rect(50f * scale, 84f * scale, 640f * scale, 24f * scale), $"{scenario.Name}  |  {Screen.width}x{Screen.height}  |  {SystemInfo.graphicsDeviceType}", labelStyle);
            GUI.Label(new Rect(50f * scale, 116f * scale, 640f * scale, 76f * scale), "影付きSpot: 512x512 / Soft Medium(9 fetch) / 同一Transform・caster・receiver\n操作: ←→ シナリオ / S 標準 / V Virtual / Space 切替 / R 現在ペア計測 / A 全計測 / P 画像保存", labelStyle);
            GUI.Label(new Rect(50f * scale, 194f * scale, 640f * scale, 52f * scale), $"状態: {state}   結果: {report.results.Count}件\n保存先: {report.outputDirectory}", runFailed ? warningStyle : labelStyle);
            DrawLatestComparison(new Rect(50f * scale, 250f * scale, 640f * scale, 70f * scale), scenario.lightCount, scenario.shadows);
            if (GUI.Button(new Rect(50f * scale, 332f * scale, 110f * scale, 34f * scale), "Standard")) SetDisplayedMode(BenchmarkLightingMode.Standard);
            if (GUI.Button(new Rect(170f * scale, 332f * scale, 110f * scale, 34f * scale), "Virtual")) SetDisplayedMode(BenchmarkLightingMode.Virtual);
            if (GUI.Button(new Rect(290f * scale, 332f * scale, 110f * scale, 34f * scale), "Measure Pair")) StartCurrentPair();
            if (GUI.Button(new Rect(410f * scale, 332f * scale, 110f * scale, 34f * scale), "Measure All")) StartAllRuns();
            if (GUI.Button(new Rect(530f * scale, 332f * scale, 110f * scale, 34f * scale), "Screenshot")) CaptureManualScreenshot();
        }

        private void InitializeReport()
        {
            var refreshRate = Screen.currentResolution.refreshRateRatio;
            var outputDirectory = options.OutputDirectory;
            if (string.IsNullOrWhiteSpace(outputDirectory)) outputDirectory = Path.Combine(Application.persistentDataPath, "VirtualLightBenchmarks", DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"));
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(Path.Combine(outputDirectory, "screenshots"));
            report = new VirtualLightBenchmarkReport
            {
                status = "ready",
                startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                buildGuid = Application.buildGUID,
                operatingSystem = SystemInfo.operatingSystem,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDriver = SystemInfo.graphicsDeviceVersion,
                processor = SystemInfo.processorType,
                width = Screen.width,
                height = Screen.height,
                refreshRateNumerator = (int)refreshRate.numerator,
                refreshRateDenominator = (int)refreshRate.denominator,
                developmentBuild = Debug.isDebugBuild,
                frameTimingStatsEnabled = true,
                standardShadowAtlasResolution = VirtualLightBenchmarkScenarios.StandardShadowAtlasResolution,
                shadowResolution = VirtualLightBenchmarkScenarios.ShadowResolution,
                equivalence = "Spot / 512x512 / Soft Medium 9 fetch / same transform, range, angles, color, intensity, caster and receiver. Storage, depth encoding, filter weights and bias remain implementation-specific.",
                outputDirectory = outputDirectory
            };
            VirtualLightBenchmarkResultWriter.Save(report);
        }

        private void StartRecorders()
        {
            cpuTotalRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Total Frame Time", 1);
            cpuMainRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Main Thread Frame Time", 1);
            cpuRenderRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Render Thread Frame Time", 1);
            gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);
        }

        private void HandleManualInput(Event currentEvent)
        {
            if (currentEvent == null || currentEvent.type != EventType.KeyDown) return;
            if (currentEvent.keyCode == KeyCode.LeftArrow) ChangePair(-1);
            if (currentEvent.keyCode == KeyCode.RightArrow) ChangePair(1);
            if (currentEvent.keyCode == KeyCode.S) SetDisplayedMode(BenchmarkLightingMode.Standard);
            if (currentEvent.keyCode == KeyCode.V) SetDisplayedMode(BenchmarkLightingMode.Virtual);
            if (currentEvent.keyCode == KeyCode.Space) SetDisplayedMode(displayedMode == BenchmarkLightingMode.Standard ? BenchmarkLightingMode.Virtual : BenchmarkLightingMode.Standard);
            if (currentEvent.keyCode == KeyCode.R) StartCurrentPair();
            if (currentEvent.keyCode == KeyCode.A) StartAllRuns();
            if (currentEvent.keyCode == KeyCode.P) CaptureManualScreenshot();
        }

        private void ChangePair(int amount)
        {
            var pairCount = scenarios.Count / 2;
            currentPairIndex = (currentPairIndex + amount + pairCount) % pairCount;
            ApplyScenario(currentPairIndex * 2 + (displayedMode == BenchmarkLightingMode.Standard ? 0 : 1));
        }

        private void SetDisplayedMode(BenchmarkLightingMode mode)
        {
            displayedMode = mode;
            ApplyScenario(currentPairIndex * 2 + (mode == BenchmarkLightingMode.Standard ? 0 : 1));
        }

        private void StartCurrentPair()
        {
            if (state != RunState.Idle) return;
            pendingRuns.Clear();
            EnqueuePair(currentPairIndex, 1);
            BeginNextRun();
        }

        private void StartAllRuns()
        {
            if (state != RunState.Idle) return;
            pendingRuns.Clear();
            for (var pairIndex = 0; pairIndex < scenarios.Count / 2; pairIndex++)
            {
                var pairScenario = scenarios[pairIndex * 2];
                if (!MatchesFilter(pairScenario)) continue;
                EnqueuePair(pairIndex, options.Repeats);
            }
            if (pendingRuns.Count == 0)
            {
                Fail($"一致するシナリオがありません: {options.Filter}");
                return;
            }
            report.status = "running";
            VirtualLightBenchmarkResultWriter.Save(report);
            BeginNextRun();
        }

        private void EnqueuePair(int pairIndex, int repeats)
        {
            for (var repetition = 0; repetition < repeats; repetition++)
            {
                if (repetition % 2 == 0)
                {
                    pendingRuns.Enqueue(new RunRequest(pairIndex * 2, repetition));
                    pendingRuns.Enqueue(new RunRequest(pairIndex * 2 + 1, repetition));
                }
                else
                {
                    pendingRuns.Enqueue(new RunRequest(pairIndex * 2 + 1, repetition));
                    pendingRuns.Enqueue(new RunRequest(pairIndex * 2, repetition));
                }
            }
        }

        private bool MatchesFilter(BenchmarkScenario scenario)
        {
            if (string.Equals(options.Filter, "all", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(options.Filter, "shadowed", StringComparison.OrdinalIgnoreCase)) return scenario.shadows;
            if (string.Equals(options.Filter, "unshadowed", StringComparison.OrdinalIgnoreCase)) return !scenario.shadows;
            return scenario.Name.IndexOf(options.Filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BeginNextRun()
        {
            currentRequest = pendingRuns.Dequeue();
            var scenario = scenarios[currentRequest.ScenarioIndex];
            currentPairIndex = currentRequest.ScenarioIndex / 2;
            displayedMode = scenario.lightingMode;
            ApplyScenario(currentRequest.ScenarioIndex);
            GC.Collect();
            stateFrame = 0;
            state = RunState.Warmup;
        }

        private void BeginSampling()
        {
            if (scenarios[currentRequest.ScenarioIndex].lightingMode == BenchmarkLightingMode.Virtual && !virtualShaderDiagnosticsLogged) LogVirtualShaderDiagnostics();
            cpuTotalSamples = new long[options.SampleFrames];
            cpuMainSamples = new long[options.SampleFrames];
            cpuRenderSamples = new long[options.SampleFrames];
            gpuSamples = new long[options.SampleFrames];
            cpuTotalValidSamples = 0;
            cpuMainValidSamples = 0;
            cpuRenderValidSamples = 0;
            gpuValidSamples = 0;
            sampleIndex = 0;
            state = RunState.Sampling;
        }

        private void CaptureProfilerValues()
        {
            CaptureRecorderValue(cpuTotalRecorder, cpuTotalSamples, ref cpuTotalValidSamples);
            CaptureRecorderValue(cpuMainRecorder, cpuMainSamples, ref cpuMainValidSamples);
            CaptureRecorderValue(cpuRenderRecorder, cpuRenderSamples, ref cpuRenderValidSamples);
            CaptureRecorderValue(gpuRecorder, gpuSamples, ref gpuValidSamples);
        }

        private static void CaptureRecorderValue(ProfilerRecorder recorder, long[] destination, ref int validCount)
        {
            if (!recorder.Valid || validCount >= destination.Length) return;
            var value = recorder.LastValue;
            if (value <= 0) return;
            destination[validCount++] = value;
        }

        private void CompleteSampling()
        {
            var scenario = scenarios[currentRequest.ScenarioIndex];
            var cpuTotal = BenchmarkStatistics.Calculate(cpuTotalSamples, cpuTotalValidSamples, 1_000_000d);
            var cpuMain = BenchmarkStatistics.Calculate(cpuMainSamples, cpuMainValidSamples, 1_000_000d);
            var cpuRender = BenchmarkStatistics.Calculate(cpuRenderSamples, cpuRenderValidSamples, 1_000_000d);
            var gpu = BenchmarkStatistics.Calculate(gpuSamples, gpuValidSamples, 1_000_000d);
            var screenshotFileName = currentRequest.Repetition == 0 ? $"{ScenarioSlug(scenario)}-{scenario.lightingMode.ToString().ToLowerInvariant()}.bmp" : string.Empty;
            var result = new VirtualLightBenchmarkResult
            {
                scenario = scenario.Name,
                backend = scenario.lightingMode.ToString(),
                lightCount = scenario.lightCount,
                shadows = scenario.shadows,
                repetition = currentRequest.Repetition,
                warmupFrames = options.WarmupFrames,
                requestedSamples = options.SampleFrames,
                sampledFrames = sampleIndex,
                cpuTotalValidSamples = cpuTotalValidSamples,
                cpuMainValidSamples = cpuMainValidSamples,
                cpuRenderValidSamples = cpuRenderValidSamples,
                gpuValidSamples = gpuValidSamples,
                gpuTimingSupported = gpuValidSamples >= Mathf.CeilToInt(options.SampleFrames * 0.95f),
                cpuTotalMedianMs = cpuTotal.Median,
                cpuTotalP95Ms = cpuTotal.P95,
                cpuMainMedianMs = cpuMain.Median,
                cpuMainP95Ms = cpuMain.P95,
                cpuRenderMedianMs = cpuRender.Median,
                cpuRenderP95Ms = cpuRender.P95,
                gpuMedianMs = gpu.Median,
                gpuP95Ms = gpu.P95,
                cpuTotalNanoseconds = CopyValid(cpuTotalSamples, cpuTotalValidSamples),
                cpuMainNanoseconds = CopyValid(cpuMainSamples, cpuMainValidSamples),
                cpuRenderNanoseconds = CopyValid(cpuRenderSamples, cpuRenderValidSamples),
                gpuNanoseconds = CopyValid(gpuSamples, gpuValidSamples),
                screenshot = screenshotFileName
            };
            report.results.Add(result);
            VirtualLightBenchmarkResultWriter.Save(report);
            if (!string.IsNullOrEmpty(screenshotFileName)) StartCoroutine(CaptureScreenshot(Path.Combine(report.outputDirectory, "screenshots", screenshotFileName), result));
            captureWaitFrames = 0;
            state = RunState.Capturing;
        }

        private void AdvanceRun()
        {
            if (pendingRuns.Count > 0)
            {
                BeginNextRun();
                return;
            }
            state = RunState.Idle;
            report.status = "complete";
            report.completedUtc = DateTime.UtcNow.ToString("O");
            VirtualLightBenchmarkResultWriter.Save(report);
            if (options.AutoRun && options.QuitWhenComplete) Application.Quit(runFailed ? 1 : 0);
        }

        private void ApplyScenario(int scenarioIndex)
        {
            var scenario = scenarios[scenarioIndex];
            for (var index = 0; index < standardLights.Length; index++)
            {
                var light = standardLights[index];
                if (light == null) continue;
                light.type = scenario.shadows ? LightType.Spot : LightType.Point;
                light.shadows = scenario.shadows ? LightShadows.Soft : LightShadows.None;
                light.enabled = scenario.lightingMode == BenchmarkLightingMode.Standard && index < scenario.lightCount;
            }
            for (var index = 0; index < virtualLights.Length; index++)
            {
                var light = virtualLights[index];
                if (light == null) continue;
                light.Type = scenario.shadows ? VirtualLightType.Spot : VirtualLightType.Point;
                light.CastShadow = scenario.shadows;
                light.enabled = scenario.lightingMode == BenchmarkLightingMode.Virtual && index < scenario.lightCount;
            }
            VirtualLightSystem.Current.SetQuality(VirtualLightQuality.Medium);
            if (standardReceivers != null) standardReceivers.SetActive(scenario.lightingMode == BenchmarkLightingMode.Standard);
            if (virtualReceivers != null) virtualReceivers.SetActive(scenario.lightingMode == BenchmarkLightingMode.Virtual);
            if (standardCasters != null) standardCasters.SetActive(scenario.lightingMode == BenchmarkLightingMode.Standard);
            if (virtualCasters != null) virtualCasters.SetActive(scenario.lightingMode == BenchmarkLightingMode.Virtual);
        }

        private void DrawLatestComparison(Rect rect, int lightCount, bool shadows)
        {
            VirtualLightBenchmarkResult standard = null;
            VirtualLightBenchmarkResult virtualResult = null;
            for (var index = report.results.Count - 1; index >= 0; index--)
            {
                var result = report.results[index];
                if (result.lightCount != lightCount || result.shadows != shadows) continue;
                if (result.backend == BenchmarkLightingMode.Standard.ToString() && standard == null) standard = result;
                if (result.backend == BenchmarkLightingMode.Virtual.ToString() && virtualResult == null) virtualResult = result;
            }
            if (standard == null || virtualResult == null)
            {
                GUI.Label(rect, "このペアは未計測です。", labelStyle);
                return;
            }
            var gpuText = standard.gpuTimingSupported && virtualResult.gpuTimingSupported ? $"GPU median: {standard.gpuMedianMs:F3} / {virtualResult.gpuMedianMs:F3} ms  ({standard.gpuMedianMs / virtualResult.gpuMedianMs:F2}x)" : "GPU timing: unsupported / insufficient samples";
            GUI.Label(rect, $"Standard / Virtual\nCPU total median: {standard.cpuTotalMedianMs:F3} / {virtualResult.cpuTotalMedianMs:F3} ms  ({standard.cpuTotalMedianMs / virtualResult.cpuTotalMedianMs:F2}x)\n{gpuText}", labelStyle);
        }

        private void CaptureManualScreenshot()
        {
            var scenario = scenarios[currentPairIndex * 2 + (displayedMode == BenchmarkLightingMode.Standard ? 0 : 1)];
            var fileName = $"manual-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{ScenarioSlug(scenario)}-{displayedMode.ToString().ToLowerInvariant()}.bmp";
            StartCoroutine(CaptureScreenshot(Path.Combine(report.outputDirectory, "screenshots", fileName), null));
        }

        private IEnumerator CaptureScreenshot(string path, VirtualLightBenchmarkResult result)
        {
            yield return new WaitForEndOfFrame();
            var width = Screen.width;
            var height = Screen.height;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false, true);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            texture.Apply(false, false);
            var pixels = texture.GetPixels32();
            var rowBytes = width * 3;
            var paddedRowBytes = (rowBytes + 3) & ~3;
            var pixelDataSize = paddedRowBytes * height;
            var bytes = new byte[54 + pixelDataSize];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            WriteInt32(bytes, 2, bytes.Length);
            WriteInt32(bytes, 10, 54);
            WriteInt32(bytes, 14, 40);
            WriteInt32(bytes, 18, width);
            WriteInt32(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = 24;
            WriteInt32(bytes, 34, pixelDataSize);
            var destination = 54;
            var magentaPixels = 0;
            var nonBlackPixels = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = pixels[y * width + x];
                    if (pixel.r > 200 && pixel.b > 200 && pixel.g < 80) magentaPixels++;
                    if (pixel.r >= 5 || pixel.g >= 5 || pixel.b >= 5) nonBlackPixels++;
                    bytes[destination++] = pixel.b;
                    bytes[destination++] = pixel.g;
                    bytes[destination++] = pixel.r;
                }
                destination += paddedRowBytes - rowBytes;
            }
            File.WriteAllBytes(path, bytes);
            Destroy(texture);
            if (result == null) yield break;
            var pixelCount = width * height;
            result.screenshotMagentaPercent = magentaPixels * 100d / pixelCount;
            result.screenshotNonBlackPercent = nonBlackPixels * 100d / pixelCount;
            VirtualLightBenchmarkResultWriter.Save(report);
            if (result.screenshotMagentaPercent > 0.1d) Fail($"Player画像でマゼンタを検出しました: {result.scenario} {result.screenshotMagentaPercent:F4}%");
        }

        private static void WriteInt32(byte[] destination, int offset, int value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private void Fail(string message)
        {
            runFailed = true;
            report.status = "failed";
            report.error = message;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            VirtualLightBenchmarkResultWriter.Save(report);
            Debug.LogError(message);
            state = RunState.Idle;
            if (options.AutoRun && options.QuitWhenComplete) Application.Quit(1);
        }

        private void LogVirtualShaderDiagnostics()
        {
            virtualShaderDiagnosticsLogged = true;
            var renderer = virtualReceivers == null ? null : virtualReceivers.GetComponentInChildren<Renderer>(true);
            var material = renderer == null ? null : renderer.sharedMaterial;
            if (material == null || material.shader == null)
            {
                Debug.LogError("Virtual benchmark material or shader is missing.");
                return;
            }
            var builder = new StringBuilder();
            builder.Append("Virtual benchmark shader: name=").Append(material.shader.name).Append(", supported=").Append(material.shader.isSupported).Append(", passCount=").Append(material.passCount).Append(", forwardPass=").Append(material.FindPass("ForwardLit")).Append(", setPass=").Append(material.SetPass(Mathf.Max(0, material.FindPass("ForwardLit")))).Append(", materialKeywords=");
            var materialKeywords = material.enabledKeywords;
            for (var index = 0; index < materialKeywords.Length; index++)
            {
                if (index > 0) builder.Append('|');
                builder.Append(materialKeywords[index].name);
            }
            builder.Append(", globalKeywords=");
            var globalKeywords = Shader.enabledGlobalKeywords;
            for (var index = 0; index < globalKeywords.Length; index++)
            {
                if (index > 0) builder.Append('|');
                builder.Append(globalKeywords[index].name);
            }
            Debug.Log(builder.ToString());
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.93f, 0.97f, 1f) } };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.68f, 0.78f, 0.9f) }, wordWrap = true };
            warningStyle = new GUIStyle(labelStyle) { normal = { textColor = new Color(1f, 0.36f, 0.26f) } };
        }

        private static long[] CopyValid(long[] values, int count)
        {
            var copy = new long[count];
            Array.Copy(values, copy, count);
            return copy;
        }

        private static string ScenarioSlug(BenchmarkScenario scenario)
        {
            return $"{(scenario.shadows ? "spot-shadow" : "point-no-shadow")}-{scenario.lightCount:D3}";
        }

        private static bool ContainsCommandLineArgument(string value)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++) if (string.Equals(arguments[index], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
