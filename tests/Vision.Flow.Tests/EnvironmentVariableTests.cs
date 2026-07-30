using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    internal static class EnvironmentVariableTests
    {
        public static Task SerializationAndPublishPreserveDefinitions()
        {
            var document = CreateDocument();
            var json = FlowDesignSerializer.Serialize(document);
            var restored = FlowDesignSerializer.Deserialize(json);
            AssertEx.Equal(3, restored.Runtime.EnvironmentVariables.Count,
                "All supported environment variable types should round-trip.");
            AssertEx.Equal(FlowDataType.Boolean,
                restored.Runtime.EnvironmentVariables[1].DataType,
                "Environment variable type should use the stable enum wire value.");
            AssertEx.Equal(VariableSelectorScope.EnvironmentVariable,
                restored.Runtime.Nodes[0].Settings[FlowSettingNames.DelayMs].Selector.Scope,
                "Environment selector scope should round-trip.");

            var result = new FlowPublishService(CreateRegistry()).Publish(restored);
            AssertEx.True(result.IsSuccess,
                "A valid environment variable flow should publish.");
            AssertEx.Equal(3, result.Runtime.EnvironmentVariables.Count,
                "Published runtime should retain environment definitions.");
            AssertEx.False(object.ReferenceEquals(
                    restored.Runtime.EnvironmentVariables[0],
                    result.Runtime.EnvironmentVariables[0]),
                "Published definitions must be deep-cloned.");
            return Task.FromResult(0);
        }

        public static Task RuntimeValuesUseDefaultsAndOverrides()
        {
            var definitions = CreateDefinitions();
            IDictionary<string, object> defaults =
                EnvironmentVariableValues.CreateSnapshot(definitions);
            AssertEx.Equal(120, defaults["timeout"],
                "Missing overrides should use typed defaults.");

            var overrides = new Dictionary<string, object>
            {
                { "timeout", "350" },
                { "enabled", false },
                { "label", "override" }
            };
            IDictionary<string, object> values =
                EnvironmentVariableValues.CreateSnapshot(definitions, overrides);
            overrides["timeout"] = 999;
            AssertEx.Equal(350, values["timeout"],
                "Environment values should be converted and isolated from caller mutation.");
            AssertEx.Throws<NotSupportedException>(
                () => values["timeout"] = 1,
                "The runtime environment snapshot should be read-only.");

            var node = new NodeDefinition { Id = "target", Type = "test" };
            node.Settings["Timeout"] = NodeSettingValue.ForVariable(
                VariableSelector.ForEnvironmentVariable("timeout"));
            var context = new FlowExecutionContext(
                new RuntimeFlowDefinition { FlowId = "environment-runtime" },
                node,
                new FlowToken(),
                new VariablePool(),
                new InMemoryFlowEventSink(),
                null,
                null,
                null,
                null,
                values);
            AssertEx.Equal(350, context.GetSettingValue<int>("Timeout"),
                "Environment selector should resolve the typed runtime value.");
            return Task.FromResult(0);
        }

        public static Task ValidatorRejectsInvalidDefinitionsAndReferences()
        {
            var document = CreateDocument();
            document.Runtime.EnvironmentVariables[1].Name = "超时时间";
            document.Runtime.EnvironmentVariables[2].DataType = FlowDataType.Object;
            document.Runtime.EnvironmentVariables[2].DefaultValue = null;
            document.Runtime.Nodes[0].Settings[FlowSettingNames.DelayMs] =
                NodeSettingValue.ForVariable(
                    VariableSelector.ForEnvironmentVariable("missing"));

            var validation = new FlowValidator(CreateRegistry())
                .Validate(document.Runtime);
            AssertEx.True(validation.Issues.Any(x =>
                    x.Code == FlowValidationIssueCodes.EnvironmentVariableNameInvalid),
                "Duplicate environment variable names should be rejected.");
            AssertEx.True(validation.Issues.Any(x =>
                    x.Code == FlowValidationIssueCodes.EnvironmentVariableTypeInvalid),
                "Unsupported environment variable types should be rejected.");
            AssertEx.True(validation.Issues.Any(x =>
                    x.Code == FlowValidationIssueCodes.EnvironmentVariableMissing),
                "Selectors must reference an existing environment variable Id.");
            return Task.FromResult(0);
        }

        public static Task DesignerSuggestionsExposeEnvironmentVariables()
        {
            Exception failure = null;
            var thread = new Thread(delegate()
            {
                try
                {
                    var control = new FlowDesignerControl(CreateRegistry());
                    var document = CreateDocument();
                    control.LoadDocumentAsync(document).GetAwaiter().GetResult();
                    var method = typeof(FlowDesignerControl).GetMethod(
                        "CreateVariableSuggestions",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    AssertEx.NotNull(method,
                        "Designer variable suggestion method should exist.");
                    var suggestions = (IList<VariableSelectionOption>)method.Invoke(
                        control,
                        new object[] { document.Runtime.Nodes[0] });
                    AssertEx.True(suggestions.Any(x =>
                            x.Selector.Scope ==
                                VariableSelectorScope.EnvironmentVariable &&
                            x.Selector.Path[0] == "timeout" &&
                            x.DataType == FlowDataType.Int32),
                        "Environment variables should be available without an upstream node.");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
                throw new InvalidOperationException(
                    "Environment variable designer verification failed.",
                    failure);
            return Task.FromResult(0);
        }

        private static FlowDesignDocument CreateDocument()
        {
            var runtime = new RuntimeFlowDefinition
            {
                FlowId = "environment-flow",
                FlowName = "Environment Flow",
                Version = "1.0.0",
                EnvironmentVariables = CreateDefinitions()
            };
            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "delay",
                Type = DelayNodeFactory.TypeName,
                Name = "延时",
                Version = "1.0.0",
                Settings =
                {
                    {
                        FlowSettingNames.DelayMs,
                        NodeSettingValue.ForVariable(
                            VariableSelector.ForEnvironmentVariable("timeout"))
                    }
                }
            });
            runtime.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "delay"
            });
            return new FlowDesignDocument
            {
                FlowId = runtime.FlowId,
                FlowName = runtime.FlowName,
                Runtime = runtime,
                View = new FlowViewState()
            };
        }

        private static List<EnvironmentVariableDefinition> CreateDefinitions()
        {
            return new List<EnvironmentVariableDefinition>
            {
                new EnvironmentVariableDefinition
                {
                    Id = "timeout",
                    Name = "超时时间",
                    DataType = FlowDataType.Int32,
                    DefaultValue = 120
                },
                new EnvironmentVariableDefinition
                {
                    Id = "enabled",
                    Name = "启用",
                    DataType = FlowDataType.Boolean,
                    DefaultValue = true
                },
                new EnvironmentVariableDefinition
                {
                    Id = "label",
                    Name = "标签",
                    DataType = FlowDataType.String,
                    DefaultValue = "default"
                }
            };
        }

        private static NodeRegistry CreateRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return registry;
        }
    }
}
