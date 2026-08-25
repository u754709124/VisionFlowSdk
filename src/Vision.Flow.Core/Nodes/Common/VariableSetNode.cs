using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Nodes
{
    /// <summary>变量写入节点配置，描述目标作用域、稳定标识和写入值。</summary>
    public sealed class VariableSetNodeConfig
    {
        /// <summary>获取或设置目标变量作用域。</summary>
        public FlowVariableTargetScope TargetScope { get; set; }

        /// <summary>获取或设置 FlowRun 局部变量名称。</summary>
        public string VariableName { get; set; }

        /// <summary>获取或设置 Session 全局变量稳定 Id。</summary>
        public string GlobalVariableId { get; set; }

        /// <summary>获取或设置回退写入值。</summary>
        public object Value { get; set; }
    }

    /// <summary>根据变量目标作用域和流程全局变量定义创建“设置变量”节点。</summary>
    public sealed class VariableSetNodeFactory :
        BaseNodeFactory<VariableSetNodeConfig>,
        IFlowDefinitionNodeDescriptorProvider
    {
        /// <summary>稳定节点类型。</summary>
        public const string TypeName = FlowNodeTypes.VariableSet;

        /// <summary>获取稳定节点类型。</summary>
        public override string NodeType
        {
            get { return TypeName; }
        }

        /// <summary>获取节点库使用的 FlowRun 模式默认描述符。</summary>
        public override NodeDescriptor Descriptor
        {
            get { return VariableSetNodeDescriptor.CreateFlowRun(); }
        }

        /// <summary>根据目标作用域和全局变量定义解析实际类型化描述符。</summary>
        public NodeDescriptor GetDescriptor(
            RuntimeFlowDefinition flow,
            NodeDefinition definition)
        {
            if (flow == null)
                throw new ArgumentNullException("flow");
            if (definition == null)
                throw new ArgumentNullException("definition");

            FlowVariableTargetScope scope = ReadTargetScope(definition);
            if (scope == FlowVariableTargetScope.FlowRun)
                return VariableSetNodeDescriptor.CreateFlowRun();

            string id = GetStringSetting(
                definition,
                FlowSettingNames.GlobalVariableId,
                null);
            GlobalVariableDefinition variable = (flow.GlobalVariables ??
                new List<GlobalVariableDefinition>()).FirstOrDefault(x =>
                    x != null && string.Equals(
                        x.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase));
            return VariableSetNodeDescriptor.CreateGlobal(
                variable,
                id,
                flow.GlobalVariables);
        }

        /// <summary>从节点稳定设置创建运行配置。</summary>
        protected override VariableSetNodeConfig CreateConfig(
            NodeDefinition definition)
        {
            return new VariableSetNodeConfig
            {
                TargetScope = ReadTargetScope(definition),
                VariableName = GetStringSetting(
                    definition,
                    FlowSettingNames.VariableName,
                    null),
                GlobalVariableId = GetStringSetting(
                    definition,
                    FlowSettingNames.GlobalVariableId,
                    null),
                Value = GetSetting(definition, FlowSettingNames.Value, null)
            };
        }

        /// <summary>创建变量写入节点实例。</summary>
        protected override IFlowNode CreateNode(
            NodeDefinition definition,
            VariableSetNodeConfig config)
        {
            return new VariableSetNode(config);
        }

        private static FlowVariableTargetScope ReadTargetScope(
            NodeDefinition definition)
        {
            object raw = GetSetting(
                definition,
                FlowSettingNames.TargetScope,
                FlowVariableTargetScope.FlowRun.ToString());
            FlowVariableTargetScope scope;
            if (!FlowEnumConverter.TryParse(raw, out scope))
            {
                throw new InvalidOperationException(
                    "TargetScope is invalid: " + Convert.ToString(raw));
            }
            return scope;
        }
    }

    /// <summary>把值写入当前 FlowRun 变量池或 Runner 共享全局存储。</summary>
    public sealed class VariableSetNode : IFlowNode
    {
        private readonly VariableSetNodeConfig _config;

        /// <summary>使用不可变配置创建节点。</summary>
        public VariableSetNode(VariableSetNodeConfig config)
        {
            _config = config ?? new VariableSetNodeConfig();
        }

        /// <summary>解析目标和值并执行一次类型安全写入。</summary>
        public Task<NodeExecutionResult> ExecuteAsync(
            FlowExecutionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_config.TargetScope == FlowVariableTargetScope.GlobalVariable)
                return Task.FromResult(SetGlobalVariable(context));

            string variableName = ResolveString(
                context,
                FlowSettingNames.VariableName,
                _config.VariableName);
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return Task.FromResult(
                    NodeExecutionResult.Failure("VariableName is required."));
            }

            object value = ResolveValue(context);
            context.Variables.Set(variableName, value);
            return Task.FromResult(Success(variableName, value));
        }

        private NodeExecutionResult SetGlobalVariable(FlowExecutionContext context)
        {
            string id = (_config.GlobalVariableId ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                return NodeExecutionResult.Failure(
                    "GlobalVariableId is required.",
                    FlowPortNames.Error,
                    NodeFailureKind.Configuration);
            }

            object value = ResolveValue(context);
            try
            {
                context.GlobalVariables.Set(id, value);
                object normalized = context.GlobalVariables.Get(id);
                GlobalVariableDefinition definition =
                    (context.Flow.GlobalVariables ??
                        new List<GlobalVariableDefinition>()).FirstOrDefault(x =>
                            x != null && string.Equals(
                                x.Id,
                                id,
                                StringComparison.OrdinalIgnoreCase));
                string name = definition == null ||
                    string.IsNullOrWhiteSpace(definition.Name)
                        ? id
                        : definition.Name;
                return Success(name, normalized);
            }
            catch (KeyNotFoundException ex)
            {
                return NodeExecutionResult.Failure(
                    ex.Message,
                    FlowPortNames.Error,
                    NodeFailureKind.Configuration);
            }
            catch (ArgumentException ex)
            {
                NodeSettingValue setting;
                bool isBinding = context.Node.Settings != null &&
                    context.Node.Settings.TryGetValue(
                        FlowSettingNames.Value,
                        out setting) &&
                    setting != null &&
                    setting.Mode == NodeSettingValueMode.Variable;
                return NodeExecutionResult.Failure(
                    ex.Message,
                    FlowPortNames.Error,
                    isBinding
                        ? NodeFailureKind.Binding
                        : NodeFailureKind.Configuration);
            }
        }

        private static NodeExecutionResult Success(string name, object value)
        {
            return NodeExecutionResult.Success(
                FlowPortNames.Next,
                new Dictionary<string, object>
                {
                    { FlowOutputNames.VariableName, name },
                    { FlowOutputNames.Value, value }
                });
        }

        private static string ResolveString(
            FlowExecutionContext context,
            string name,
            string defaultValue)
        {
            object value = context.GetSettingValue(name);
            return value == null ? defaultValue : Convert.ToString(value);
        }

        private object ResolveValue(FlowExecutionContext context)
        {
            if (context.Node.Settings != null &&
                context.Node.Settings.ContainsKey(FlowSettingNames.Value))
            {
                return context.GetSettingValue(FlowSettingNames.Value);
            }
            return _config.Value;
        }
    }

    /// <summary>创建局部或全局写入模式的动态节点描述符。</summary>
    public static class VariableSetNodeDescriptor
    {
        /// <summary>创建向后兼容的 FlowRun 局部变量描述符。</summary>
        public static NodeDescriptor CreateFlowRun()
        {
            return Create(
                FlowVariableTargetScope.FlowRun,
                null,
                FlowDataType.Object);
        }

        /// <summary>创建目标类型与指定全局变量一致的描述符。</summary>
        public static NodeDescriptor CreateGlobal(
            GlobalVariableDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            return CreateGlobal(definition, definition.Id, new[] { definition });
        }

        internal static NodeDescriptor CreateGlobal(
            GlobalVariableDefinition definition,
            string selectedId,
            IEnumerable<GlobalVariableDefinition> definitions)
        {
            var availableIds = new HashSet<string>(
                (definitions ?? Enumerable.Empty<GlobalVariableDefinition>())
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                    .Select(x => x.Id),
                StringComparer.OrdinalIgnoreCase);
            var descriptor = Create(
                FlowVariableTargetScope.GlobalVariable,
                definition,
                definition == null ? FlowDataType.Object : definition.DataType);
            var idSetting = descriptor.Settings.First(x =>
                string.Equals(x.Name, FlowSettingNames.GlobalVariableId, StringComparison.OrdinalIgnoreCase));
            idSetting.DefaultValue = selectedId;
            idSetting.Validator = value =>
            {
                var id = Convert.ToString(value);
                return string.IsNullOrWhiteSpace(id) || !availableIds.Contains(id)
                    ? "目标全局变量不存在：" + id
                    : null;
            };
            return descriptor;
        }

        private static NodeDescriptor Create(
            FlowVariableTargetScope scope,
            GlobalVariableDefinition definition,
            FlowDataType valueType)
        {
            var descriptor = new NodeDescriptor
            {
                NodeType = VariableSetNodeFactory.TypeName,
                DisplayName = "设置变量",
                Category = "通用",
                Version = "2.0.0",
                Description = scope == FlowVariableTargetScope.FlowRun
                    ? "将值写入当前流程运行的局部变量池。"
                    : "将值写入当前 Session 共享的全局变量。"
            };
            descriptor.InputPorts.Add(new NodePortDescriptor
            {
                Name = FlowPortNames.In,
                DisplayName = FlowPortNames.In,
                Direction = FlowPortDirection.Input,
                DataType = FlowDataType.Control,
                IsRequired = true
            });
            descriptor.OutputPorts.Add(new NodePortDescriptor
            {
                Name = FlowPortNames.Next,
                DisplayName = FlowPortNames.Next,
                Direction = FlowPortDirection.Output,
                DataType = FlowDataType.Control
            });
            descriptor.OutputPorts.Add(new NodePortDescriptor
            {
                Name = FlowPortNames.Error,
                DisplayName = FlowPortNames.Error,
                Direction = FlowPortDirection.Output,
                DataType = FlowDataType.Control
            });
            descriptor.Settings.Add(new NodeSettingDescriptor
            {
                Name = FlowSettingNames.TargetScope,
                DisplayName = "目标作用域",
                DataType = FlowDataType.String,
                EnumType = typeof(FlowVariableTargetScope),
                DefaultValue = FlowEnumConverter.ToWireValue(scope),
                IsRequired = false,
                BindingMode = NodeSettingBindingMode.ConstantOnly,
                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                AllowedVariableSources = VariableSelectorScopeFlags.None,
                AffectsDescriptor = true
            });
            if (scope == FlowVariableTargetScope.FlowRun)
            {
                descriptor.Settings.Add(new NodeSettingDescriptor
                {
                    Name = FlowSettingNames.VariableName,
                    DisplayName = "变量名称",
                    DataType = FlowDataType.String,
                    IsRequired = true,
                    BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                    EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                    AllowedVariableSources = VariableSelectorScopeFlags.All
                });
            }
            else
            {
                descriptor.Settings.Add(new NodeSettingDescriptor
                {
                    Name = FlowSettingNames.GlobalVariableId,
                    DisplayName = "全局变量",
                    DataType = FlowDataType.String,
                    DefaultValue = definition == null ? null : definition.Id,
                    IsRequired = true,
                    BindingMode = NodeSettingBindingMode.ConstantOnly,
                    EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                    AllowedVariableSources = VariableSelectorScopeFlags.None,
                    AffectsDescriptor = true
                });
            }
            descriptor.Settings.Add(new NodeSettingDescriptor
            {
                Name = FlowSettingNames.Value,
                DisplayName = "写入值",
                DataType = valueType,
                IsRequired = scope == FlowVariableTargetScope.GlobalVariable,
                BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                AllowedVariableSources = scope == FlowVariableTargetScope.FlowRun
                    ? VariableSelectorScopeFlags.All
                    : VariableSelectorScopeFlags.NodeOutput
            });
            descriptor.Outputs.Add(new NodeOutputDescriptor
            {
                Name = FlowOutputNames.VariableName,
                DisplayName = "变量名称",
                DataType = FlowDataType.String
            });
            descriptor.Outputs.Add(new NodeOutputDescriptor
            {
                Name = FlowOutputNames.Value,
                DisplayName = "变量值",
                DataType = valueType
            });
            return descriptor;
        }
    }
}
