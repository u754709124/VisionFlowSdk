using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // 数据库保存节点将流程变量映射为适配器字段值。
    public sealed class DatabaseFieldMappingConfig
    {
        public string FieldName { get; set; }

        public string InputName { get; set; }

        public object Value { get; set; }

        public string ValueBinding { get; set; }
    }

    public sealed class DatabaseSaveNodeConfig
    {
        public DatabaseSaveNodeConfig()
        {
            FieldMappings = new List<DatabaseFieldMappingConfig>();
            Queue = new AdapterNodeQueueConfig
            {
                QueueName = FlowQueueNames.DatabaseSave
            };
        }

        public string DatabaseId { get; set; }

        public string TableName { get; set; }

        public IList<DatabaseFieldMappingConfig> FieldMappings { get; set; }

        public AdapterNodeQueueConfig Queue { get; set; }
    }

    public sealed class DatabaseSaveNodeFactory : BaseNodeFactory<DatabaseSaveNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.DatabaseSave;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return DatabaseSaveNodeDescriptor.Create(); }
        }

        protected override DatabaseSaveNodeConfig CreateConfig(NodeDefinition definition)
        {
            var config = new DatabaseSaveNodeConfig
            {
                DatabaseId = GetStringSetting(definition, "DatabaseId", null),
                TableName = GetStringSetting(definition, "TableName", null),
                Queue = AdapterNodeHelpers.CreateQueueConfig(definition, FlowQueueNames.DatabaseSave)
            };
            AdapterNodeHelpers.AddFieldMappings(config.FieldMappings, GetSetting(definition, "FieldMappings", null));
            return config;
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, DatabaseSaveNodeConfig config)
        {
            return new DatabaseSaveNode(config);
        }
    }

    public sealed class DatabaseSaveNode : IFlowNode
    {
        private readonly DatabaseSaveNodeConfig _config;

        public DatabaseSaveNode(DatabaseSaveNodeConfig config)
        {
            _config = config ?? new DatabaseSaveNodeConfig();
            if (_config.FieldMappings == null)
            {
                _config.FieldMappings = new List<DatabaseFieldMappingConfig>();
            }

            if (_config.Queue == null)
            {
                _config.Queue = new AdapterNodeQueueConfig { QueueName = FlowQueueNames.DatabaseSave };
            }
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var databaseId = AdapterNodeHelpers.ResolveString(context, "DatabaseId", _config.DatabaseId);
            if (string.IsNullOrWhiteSpace(databaseId))
            {
                return NodeExecutionResult.Failure("DatabaseId is required.");
            }

            var tableName = AdapterNodeHelpers.ResolveString(context, "TableName", _config.TableName);
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return NodeExecutionResult.Failure("TableName is required.");
            }

            var values = CreateValues(context);
            var database = context.Devices.GetDatabase(databaseId);
            var request = new DatabaseSaveRequest
            {
                DatabaseId = databaseId,
                TableName = tableName,
                Values = values
            };
            AdapterNodeHelpers.AddTokenMetadata(context.Token, request.Metadata);
            request.Metadata[FlowMetadataKeys.NodeId] = context.Node.Id;

            var queueResult = await AdapterNodeHelpers.ExecuteWithOptionalQueueResultAsync<object>(
                context,
                _config.Queue,
                FlowQueueNames.DatabaseSave,
                FlowNodeTypes.DatabaseSave,
                async delegate(CancellationToken token)
                {
                    await database.SaveAsync(request, token).ConfigureAwait(false);
                    return null;
                },
                cancellationToken).ConfigureAwait(false);

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "Saved", queueResult.WaitedForCompletion },
                    { "Queued", queueResult.IsQueued },
                    { "QueueCompleted", queueResult.WaitedForCompletion }
                });
        }

        private IDictionary<string, object> CreateValues(FlowExecutionContext context)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (_config.FieldMappings.Count > 0)
            {
                foreach (var mapping in _config.FieldMappings)
                {
                    if (mapping == null || string.IsNullOrWhiteSpace(mapping.FieldName))
                    {
                        continue;
                    }

                    values[mapping.FieldName] = AdapterNodeHelpers.ResolveFieldValue(context, mapping);
                }

                return values;
            }

            if (context.Node.InputBindings != null)
            {
                foreach (var input in context.Node.InputBindings)
                {
                    values[input.Key] = context.GetInputValue(input.Key);
                }
            }

            return values;
        }
    }

    public static class DatabaseSaveNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = DatabaseSaveNodeFactory.TypeName,
                DisplayName = "Save Database Row",
                Category = "Storage",
                Version = "1.0.0",
                Description = "Saves inspection values through a database adapter.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid configuration or adapter errors.")
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "DatabaseId",
                        DisplayName = "Database",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered database adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TableName",
                        DisplayName = "Table",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Target table name."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "FieldMappings",
                        DisplayName = "Field Mappings",
                        DataType = "Object",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Field mappings with FieldName and Value, ValueBinding, or InputName."
                    },
                    AdapterNodeDescriptors.QueueUseSetting(),
                    AdapterNodeDescriptors.QueueNameSetting(FlowQueueNames.DatabaseSave),
                    AdapterNodeDescriptors.QueueCapacitySetting(),
                    AdapterNodeDescriptors.QueueMaxDegreeSetting(),
                    AdapterNodeDescriptors.QueueFullModeSetting(),
                    AdapterNodeDescriptors.QueueWaitForCompletionSetting()
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "Saved",
                        DisplayName = "Saved",
                        DataType = "Boolean",
                        Description = "True when the adapter save call completes."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Queued",
                        DisplayName = "Queued",
                        DataType = "Boolean",
                        Description = "True when database save was accepted by a queue without waiting."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "QueueCompleted",
                        DisplayName = "Queue Completed",
                        DataType = "Boolean",
                        Description = "True when queued database save work completed before node output."
                    }
                }
            };
        }
    }
}
