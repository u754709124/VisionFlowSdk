using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // 示例流程测试确保仓库内 Demo 资产仍能反序列化、校验和发布。
    internal static class SampleFlowTests
    {
        public static Task SampleFlowFilesDeserializeAndValidate()
        {
            var sampleDirectory = GetSampleDirectory();
            var registry = CreateRegistry();
            var validator = new FlowValidator(registry);
            var publisher = new FlowPublishService(registry);

            ValidateRuntimeFile(Path.Combine(sampleDirectory, "single-shot" + FlowFileExtensions.FlowRuntime), validator);

            var designFiles = new[]
            {
                "single-shot" + FlowFileExtensions.FlowDesign,
                "two-position-stitch" + FlowFileExtensions.FlowDesign,
                "continuous-scan" + FlowFileExtensions.FlowDesign
            };

            for (var index = 0; index < designFiles.Length; index++)
            {
                var path = Path.Combine(sampleDirectory, designFiles[index]);
                AssertEx.True(File.Exists(path), "Sample design should exist: " + path);

                var document = FlowDesignSerializer.Load(path);
                AssertEx.NotNull(document, "Sample design should deserialize: " + path);
                AssertEx.NotNull(document.Runtime, "Sample design should include runtime: " + path);
                AssertEx.NotNull(document.View, "Sample design should include view state: " + path);
                AssertEx.True(document.View.Nodes.Count > 0, "Sample design should include node coordinates: " + path);

                var publishResult = publisher.Publish(document);
                AssertValid(publishResult.Validation, path);
                AssertEx.NotNull(publishResult.Runtime, "Published sample runtime should be available: " + path);

                var runtimeJson = RuntimeFlowSerializer.Serialize(publishResult.Runtime);
                AssertNoViewState(runtimeJson, path);
            }

            return Task.FromResult(0);
        }

        public static Task ContinuousScanPublishesRuntimeWithEnhancedRules()
        {
            var sampleDirectory = GetSampleDirectory();
            var path = Path.Combine(sampleDirectory, "continuous-scan" + FlowFileExtensions.FlowDesign);
            var document = FlowDesignSerializer.Load(path);
            var result = new FlowPublishService(CreateRegistry()).Publish(document);

            AssertValid(result.Validation, path);
            AssertEx.NotNull(result.Runtime, "Published continuous scan runtime should be available.");

            var scanJoin = result.Runtime.Nodes.FirstOrDefault(x => string.Equals(x.Id, "scan_join_1", StringComparison.OrdinalIgnoreCase));
            AssertEx.NotNull(scanJoin, "Continuous scan sample should contain scan_join_1.");
            AssertEx.Equal(true, Convert.ToBoolean(scanJoin.Settings["RequireContinuousFrameIndex"], CultureInfo.InvariantCulture), "Sample scan join should require continuous frame indexes.");
            AssertEx.Equal("Replace", Convert.ToString(scanJoin.Settings["DuplicatePolicy"], CultureInfo.InvariantCulture), "Sample scan join should use Replace duplicate policy.");

            var runtimeJson = RuntimeFlowSerializer.Serialize(result.Runtime);
            AssertNoViewState(runtimeJson, path);
            AssertEx.False(runtimeJson.IndexOf("\"runtime\"", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime JSON should not contain a nested runtime document.");
            return Task.FromResult(0);
        }

        public static Task SampleRuntimeExcludesViewState()
        {
            var sampleDirectory = GetSampleDirectory();
            var runtimePath = Path.Combine(sampleDirectory, "single-shot" + FlowFileExtensions.FlowRuntime);
            AssertEx.True(File.Exists(runtimePath), "Sample runtime should exist: " + runtimePath);

            var runtime = RuntimeFlowSerializer.Load(runtimePath);
            AssertEx.NotNull(runtime, "Sample runtime should deserialize.");
            AssertEx.Equal("single-shot", runtime.FlowId, "Sample runtime FlowId should match.");

            var runtimeJson = File.ReadAllText(runtimePath);
            AssertNoViewState(runtimeJson, runtimePath);
            return Task.FromResult(0);
        }

        private static void ValidateRuntimeFile(string path, FlowValidator validator)
        {
            AssertEx.True(File.Exists(path), "Sample runtime should exist: " + path);
            var runtime = RuntimeFlowSerializer.Load(path);
            AssertEx.NotNull(runtime, "Sample runtime should deserialize: " + path);
            AssertValid(validator.Validate(runtime), path);
        }

        private static NodeRegistry CreateRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return registry;
        }

        private static string GetSampleDirectory()
        {
            var roots = new[]
            {
                Environment.CurrentDirectory,
                AppDomain.CurrentDomain.BaseDirectory
            };

            for (var index = 0; index < roots.Length; index++)
            {
                var root = roots[index];
                for (var depth = 0; depth < 10 && !string.IsNullOrWhiteSpace(root); depth++)
                {
                    var candidate = Path.Combine(root, "samples", "flows");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    var parent = Directory.GetParent(root);
                    root = parent == null ? null : parent.FullName;
                }
            }

            throw new InvalidOperationException("Could not locate samples/flows from current test directory.");
        }

        private static void AssertValid(FlowValidationResult result, string path)
        {
            AssertEx.NotNull(result, "Validation result should be available: " + path);
            if (!result.IsValid)
            {
                var issues = string.Join(
                    "; ",
                    result.Issues.Select(x =>
                        x.Severity + " " +
                        x.Code + " node=" +
                        x.NodeId + " edge=" +
                        x.EdgeIndex + " entry=" +
                        x.EntryName + " field=" +
                        x.Field + " " +
                        x.Message).ToArray());
                throw new InvalidOperationException("Sample flow should validate: " + path + ". Issues: " + issues);
            }
        }

        private static void AssertNoViewState(string json, string path)
        {
            AssertEx.False(json.IndexOf("\"view\"", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain view object: " + path);
            AssertEx.False(json.IndexOf("\"zoom\"", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain canvas zoom: " + path);
            AssertEx.False(json.IndexOf("\"offsetX\"", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain canvas offset: " + path);
            AssertEx.False(json.IndexOf("\"nodes\"", StringComparison.OrdinalIgnoreCase) >= 0 && json.IndexOf("\"x\"", StringComparison.OrdinalIgnoreCase) >= 0 && json.IndexOf("\"y\"", StringComparison.OrdinalIgnoreCase) >= 0 && json.IndexOf("\"runtime\"", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain designer node coordinates: " + path);
            AssertEx.False(json.IndexOf("NodeViewState", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain designer view types: " + path);
        }
    }
}
