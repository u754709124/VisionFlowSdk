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
    // 第 07 阶段测试覆盖配方、保存、数据库以及启用队列的适配器节点链路。
    internal static class Stage07NodeTests
    {
        public static async Task CallbackRecipeSaveDatabaseFlow()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var light = new FakeLightAdapter("Light01");
            var recipe = new FakeRecipeAdapter("Recipe01");
            var resultImage = new FakeVisionImage("result-image-001", 640, 480, "RGB24", null);
            recipe.DefaultOutputs["ResultImage"] = resultImage;
            recipe.DefaultOutputs["IsOk"] = true;

            var saver = new FakeImageSaveAdapter("ImageSave01");
            var database = new FakeDatabaseAdapter("VisionDb");
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);
            devices.RegisterLight(light);
            devices.RegisterRecipe(recipe);
            devices.RegisterImageSaver(saver);
            devices.RegisterDatabase(database);

            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink, devices).CreateRunner(CreateStage07Flow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync(
                "ManualStart",
                new FlowToken
                {
                    TokenId = "token-stage07",
                    ProductId = "P-007",
                    WorkpieceId = "W-007"
                }).ConfigureAwait(false);

            var lightSnapshot = light.Snapshot();
            AssertEx.True(lightSnapshot.ContainsKey("CH1"), "LightControlNode should set CH1.");
            AssertEx.True(lightSnapshot["CH1"].IsEnabled, "LightControlNode should enable CH1.");
            AssertEx.Equal(75.0, lightSnapshot["CH1"].Intensity, "LightControlNode should set CH1 intensity.");

            var callbackImage = FindOutput(sink, "callback1", "Image") as IVisionImage;
            var frameId = Convert.ToString(FindOutput(sink, "callback1", "FrameId"));
            var recipeResult = FindOutput(sink, "recipe1", "Result") as RecipeRunResult;
            var isOk = Convert.ToBoolean(FindOutput(sink, "recipe1", "IsOk"));
            var recipeResultImage = FindOutput(sink, "recipe1", "ResultImage") as IVisionImage;

            AssertEx.NotNull(callbackImage, "Camera callback should output an image for the stage 07 chain.");
            AssertEx.NotNull(recipeResult, "RecipeRunNode should output the adapter result.");
            AssertEx.True(isOk, "RecipeRunNode should output IsOk from the fake recipe.");
            AssertEx.True(object.ReferenceEquals(resultImage, recipeResultImage), "RecipeRunNode should output the fake recipe result image.");
            AssertEx.True(object.ReferenceEquals(callbackImage, recipeResult.Outputs["Input.InputImage"]), "RecipeRunNode should pass InputImage through variable binding.");

            var imagePath = Convert.ToString(FindOutput(sink, "save1", "ImagePath"));
            var resultImagePath = Convert.ToString(FindOutput(sink, "save1", "ResultImagePath"));
            var expectedDirectory = "fake://images/Camera01/OK";
            AssertEx.Equal(expectedDirectory + "/" + frameId + ".png", imagePath, "ImageSaveNode should output the raw image save path.");
            AssertEx.Equal(expectedDirectory + "/" + frameId + "_result.png", resultImagePath, "ImageSaveNode should output the result image save path.");

            var savedImages = saver.SnapshotSavedRequests();
            AssertEx.Equal(2, savedImages.Count, "ImageSaveNode should call the image saver for raw and result images.");
            AssertEx.Equal(expectedDirectory, savedImages[0].DirectoryPath, "Raw image save request should use the rendered directory.");
            AssertEx.Equal(frameId + ".png", savedImages[0].FileName, "Raw image save request should use the rendered file name.");
            AssertEx.Equal("Image", Convert.ToString(savedImages[0].Metadata[FlowMetadataKeys.Role]), "Raw image save request should be marked as Image.");
            AssertEx.Equal(frameId + "_result.png", savedImages[1].FileName, "Result image save request should use a result file name.");
            AssertEx.Equal("ResultImage", Convert.ToString(savedImages[1].Metadata[FlowMetadataKeys.Role]), "Result image save request should be marked as ResultImage.");

            var dbSaved = Convert.ToBoolean(FindOutput(sink, "db1", "Saved"));
            var savedRows = database.SnapshotSavedRequests();
            AssertEx.True(dbSaved, "DatabaseSaveNode should output Saved=true.");
            AssertEx.Equal(1, savedRows.Count, "DatabaseSaveNode should call the database adapter once.");
            AssertEx.Equal("InspectionResult", savedRows[0].TableName, "DatabaseSaveNode should use the configured table.");
            AssertEx.Equal(frameId, Convert.ToString(savedRows[0].Values["FrameId"]), "DatabaseSaveNode should save the bound FrameId.");
            AssertEx.Equal(imagePath, Convert.ToString(savedRows[0].Values["ImagePath"]), "DatabaseSaveNode should save the bound ImagePath.");
            AssertEx.Equal(resultImagePath, Convert.ToString(savedRows[0].Values["ResultImagePath"]), "DatabaseSaveNode should save the bound ResultImagePath.");
            AssertEx.True(Convert.ToBoolean(savedRows[0].Values["IsOk"]), "DatabaseSaveNode should save the bound IsOk value.");
        }

        public static async Task QueuedRecipeSaveDatabaseFlow()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var light = new FakeLightAdapter("Light01");
            var recipe = new FakeRecipeAdapter("Recipe01");
            recipe.DefaultOutputs["ResultImage"] = new FakeVisionImage("result-image-queued", 640, 480, "RGB24", null);
            recipe.DefaultOutputs["IsOk"] = true;

            var saver = new FakeImageSaveAdapter("ImageSave01");
            var database = new FakeDatabaseAdapter("VisionDb");
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);
            devices.RegisterLight(light);
            devices.RegisterRecipe(recipe);
            devices.RegisterImageSaver(saver);
            devices.RegisterDatabase(database);

            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink, devices).CreateRunner(CreateQueuedStage07Flow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync(
                "ManualStart",
                new FlowToken
                {
                    TokenId = "token-stage07-queued",
                    ProductId = "P-007",
                    WorkpieceId = "W-007"
                }).ConfigureAwait(false);

            AssertQueueCompleted(sink, "recipe1", FlowQueueNames.Recipe, FlowNodeTypes.RecipeRun, 1);
            AssertQueueCompleted(sink, "save1", FlowQueueNames.ImageSave, FlowNodeTypes.ImageSave + "." + FlowOutputNames.Image, 1);
            AssertQueueCompleted(sink, "save1", FlowQueueNames.ImageSave, FlowNodeTypes.ImageSave + "." + FlowOutputNames.ResultImage, 1);
            AssertQueueCompleted(sink, "db1", FlowQueueNames.DatabaseSave, FlowNodeTypes.DatabaseSave, 1);

            AssertEx.NotNull(FindOutput(sink, "recipe1", "Result"), "Queued RecipeRunNode should still output Result.");
            AssertEx.NotNull(FindOutput(sink, "save1", "ImagePath"), "Queued ImageSaveNode should still output ImagePath.");
            AssertEx.True(Convert.ToBoolean(FindOutput(sink, "db1", "Saved")), "Queued DatabaseSaveNode should still output Saved=true.");
            AssertEx.Equal(2, saver.SnapshotSavedRequests().Count, "Queued ImageSaveNode should save raw and result images.");
            AssertEx.Equal(1, database.SnapshotSavedRequests().Count, "Queued DatabaseSaveNode should save one row.");
        }

        public static async Task NonBlockingSaveAndDatabaseQueues()
        {
            var saver = new FakeImageSaveAdapter("ImageSave01")
            {
                DelayMs = 250
            };
            var database = new FakeDatabaseAdapter("VisionDb")
            {
                DelayMs = 250
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterImageSaver(saver);
            devices.RegisterDatabase(database);

            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink, devices).CreateRunner(CreateNonBlockingSaveDatabaseFlow());
            var token = new FlowToken
            {
                TokenId = "token-nonblocking-queue"
            };

            await runner.StartAsync().ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);
            stopwatch.Stop();

            AssertEx.True(stopwatch.ElapsedMilliseconds < 220, "WaitForCompletion=false should return before delayed background work completes.");
            AssertEx.True(Convert.ToBoolean(FindOutput(sink, "save1", "Queued"), CultureInfo.InvariantCulture), "ImageSaveNode should output Queued=true.");
            AssertEx.True(Convert.ToBoolean(FindOutput(sink, "save1", "ImageQueued"), CultureInfo.InvariantCulture), "ImageSaveNode should output ImageQueued=true.");
            AssertEx.True(Convert.ToBoolean(FindOutput(sink, "db1", "Queued"), CultureInfo.InvariantCulture), "DatabaseSaveNode should output Queued=true.");
            AssertEx.False(Convert.ToBoolean(FindOutput(sink, "db1", "Saved"), CultureInfo.InvariantCulture), "DatabaseSaveNode should output Saved=false before background completion.");

            await WaitForQueueCompletedAsync(sink, "save1", FlowQueueNames.ImageSave, FlowNodeTypes.ImageSave + "." + FlowOutputNames.Image, 1).ConfigureAwait(false);
            await WaitForQueueCompletedAsync(sink, "db1", FlowQueueNames.DatabaseSave, FlowNodeTypes.DatabaseSave, 1).ConfigureAwait(false);

            AssertEx.Equal(1, saver.SnapshotSavedRequests().Count, "Non-blocking ImageSaveNode should eventually save one image.");
            AssertEx.Equal(1, database.SnapshotSavedRequests().Count, "Non-blocking DatabaseSaveNode should eventually save one row.");
        }

        public static async Task NonBlockingRecipeQueue()
        {
            var recipe = new FakeRecipeAdapter("Recipe01")
            {
                DelayMs = 250
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterRecipe(recipe);

            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink, devices).CreateRunner(CreateNonBlockingRecipeFlow());

            await runner.StartAsync().ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-nonblocking-recipe" }).ConfigureAwait(false);
            stopwatch.Stop();

            AssertEx.True(stopwatch.ElapsedMilliseconds < 220, "Non-blocking RecipeRunNode should return before delayed recipe execution completes.");
            AssertEx.True(Convert.ToBoolean(FindOutput(sink, "recipe1", "Queued"), CultureInfo.InvariantCulture), "RecipeRunNode should output Queued=true.");
            AssertEx.False(Convert.ToBoolean(FindOutput(sink, "recipe1", "QueueCompleted"), CultureInfo.InvariantCulture), "RecipeRunNode should output QueueCompleted=false before background completion.");

            await WaitForQueueCompletedAsync(sink, "recipe1", FlowQueueNames.Recipe, FlowNodeTypes.RecipeRun, 1).ConfigureAwait(false);
        }

        private static RuntimeFlowDefinition CreateStage07Flow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage07-chain",
                FlowName = "Stage 07 Chain",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "light1",
                Type = LightControlNodeFactory.TypeName,
                Name = "Light Control",
                Version = "1.0.0",
                Settings =
                {
                    { "LightId", "Light01" },
                    { "StableDelayMs", 1 },
                    {
                        "Channels",
                        new[]
                        {
                            new LightChannelControlConfig
                            {
                                ChannelName = "CH1",
                                IsEnabled = true,
                                Intensity = 75.0,
                                DurationMs = 10
                            }
                        }
                    }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "trigger1",
                Type = CameraSoftTriggerNodeFactory.TypeName,
                Name = "Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "MatchMode", "TriggerId" },
                    { "TimeoutMs", 1000 }
                },
                InputBindings =
                {
                    { "TriggerId", VariableBinding.ForVariable("trigger1", "TriggerId") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "recipe1",
                Type = RecipeRunNodeFactory.TypeName,
                Name = "Recipe Run",
                Version = "1.0.0",
                Settings =
                {
                    { "RecipeId", "Recipe01" },
                    { "TimeoutMs", 1000 }
                },
                InputBindings =
                {
                    { "InputImage", VariableBinding.ForVariable("callback1", "Image") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "save1",
                Type = ImageSaveNodeFactory.TypeName,
                Name = "Image Save",
                Version = "1.0.0",
                Settings =
                {
                    { "SaverId", "ImageSave01" },
                    { "RootDirectory", "fake://images" },
                    { "DirectoryTemplate", "{CameraId}/{Result}" },
                    { "FileNameTemplate", "{FrameId}.png" }
                },
                InputBindings =
                {
                    { "Image", VariableBinding.ForVariable("callback1", "Image") },
                    { "ResultImage", VariableBinding.ForVariable("recipe1", "ResultImage") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "db1",
                Type = DatabaseSaveNodeFactory.TypeName,
                Name = "Database Save",
                Version = "1.0.0",
                Settings =
                {
                    { "DatabaseId", "VisionDb" },
                    { "TableName", "InspectionResult" },
                    {
                        "FieldMappings",
                        new[]
                        {
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "FrameId",
                                ValueBinding = "{{ callback1.FrameId }}"
                            },
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "ImagePath",
                                ValueBinding = "{{ save1.ImagePath }}"
                            },
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "ResultImagePath",
                                ValueBinding = "{{ save1.ResultImagePath }}"
                            },
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "IsOk",
                                ValueBinding = "{{ recipe1.IsOk }}"
                            }
                        }
                    }
                }
            });

            flow.Edges.Add(CreateEdge("light1", "Next", "trigger1"));
            flow.Edges.Add(CreateEdge("trigger1", "Next", "callback1"));
            flow.Edges.Add(CreateEdge("callback1", "Next", "recipe1"));
            flow.Edges.Add(CreateEdge("recipe1", "Next", "save1"));
            flow.Edges.Add(CreateEdge("save1", "Next", "db1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "light1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateQueuedStage07Flow()
        {
            var flow = CreateStage07Flow();
            EnableQueue(flow, "recipe1", FlowQueueNames.Recipe);
            EnableQueue(flow, "save1", FlowQueueNames.ImageSave);
            EnableQueue(flow, "db1", FlowQueueNames.DatabaseSave);
            return flow;
        }

        private static RuntimeFlowDefinition CreateNonBlockingSaveDatabaseFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage07-nonblocking-queue",
                FlowName = "Stage 07 Non-blocking Queue",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "save1",
                Type = ImageSaveNodeFactory.TypeName,
                Name = "Save Image",
                Version = "1.0.0",
                Settings =
                {
                    { "SaverId", "ImageSave01" },
                    { "FileNameTemplate", "{ImageId}.png" },
                    { "UseQueue", true },
                    { FlowSettingNames.QueueName, FlowQueueNames.ImageSave },
                    { "QueueCapacity", 4 },
                    { "QueueMaxDegreeOfParallelism", 1 },
                    { "QueueFullMode", "Reject" },
                    { "WaitForCompletion", false }
                },
                InputBindings =
                {
                    { "Image", VariableBinding.ForConstant(new FakeVisionImage("queued-image", 320, 240, "Mono8", new byte[] { 1, 2, 3 })) }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "db1",
                Type = DatabaseSaveNodeFactory.TypeName,
                Name = "Save Database",
                Version = "1.0.0",
                Settings =
                {
                    { "DatabaseId", "VisionDb" },
                    { "TableName", "InspectionResult" },
                    { "UseQueue", true },
                    { FlowSettingNames.QueueName, FlowQueueNames.DatabaseSave },
                    { "QueueCapacity", 4 },
                    { "QueueMaxDegreeOfParallelism", 1 },
                    { "QueueFullMode", "Reject" },
                    { "WaitForCompletion", false },
                    {
                        "FieldMappings",
                        new[]
                        {
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "TokenId",
                                Value = "token-nonblocking-queue"
                            }
                        }
                    }
                }
            });

            flow.Edges.Add(CreateEdge("save1", "Next", "db1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "save1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateNonBlockingRecipeFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage07-nonblocking-recipe",
                FlowName = "Stage 07 Non-blocking Recipe",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "recipe1",
                Type = RecipeRunNodeFactory.TypeName,
                Name = "Run Recipe",
                Version = "1.0.0",
                Settings =
                {
                    { "RecipeId", "Recipe01" },
                    { "UseQueue", true },
                    { FlowSettingNames.QueueName, FlowQueueNames.Recipe },
                    { "QueueCapacity", 4 },
                    { "QueueMaxDegreeOfParallelism", 1 },
                    { "QueueFullMode", "Reject" },
                    { "WaitForCompletion", false },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "recipe1" });
            return flow;
        }

        private static void EnableQueue(RuntimeFlowDefinition flow, string nodeId, string queueName)
        {
            var node = flow.Nodes.First(x => string.Equals(x.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            node.Settings["UseQueue"] = true;
            node.Settings["QueueName"] = queueName;
            node.Settings["QueueCapacity"] = 8;
            node.Settings["QueueMaxDegreeOfParallelism"] = 1;
            node.Settings["QueueFullMode"] = "Reject";
        }

        private static void AssertQueueCompleted(
            InMemoryFlowEventSink sink,
            string nodeId,
            string queueName,
            string operationName,
            int expectedCount)
        {
            var count = sink.Events.Count(x =>
                x.EventType == FlowRuntimeEventType.QueueCompleted &&
                string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.QueueName], CultureInfo.InvariantCulture), queueName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.OperationName], CultureInfo.InvariantCulture), operationName, StringComparison.OrdinalIgnoreCase));

            AssertEx.Equal(expectedCount, count, "QueueCompleted event count should match for " + nodeId + " / " + operationName + ".");
        }

        private static async Task WaitForQueueCompletedAsync(
            InMemoryFlowEventSink sink,
            string nodeId,
            string queueName,
            string operationName,
            int expectedCount)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var count = sink.Events.Count(x =>
                    x.EventType == FlowRuntimeEventType.QueueCompleted &&
                    string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.QueueName], CultureInfo.InvariantCulture), queueName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.OperationName], CultureInfo.InvariantCulture), operationName, StringComparison.OrdinalIgnoreCase));

                if (count >= expectedCount)
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            AssertQueueCompleted(sink, nodeId, queueName, operationName, expectedCount);
        }

        private static EdgeDefinition CreateEdge(string fromNodeId, string fromPort, string toNodeId)
        {
            return new EdgeDefinition
            {
                FromNodeId = fromNodeId,
                FromPort = fromPort,
                ToNodeId = toNodeId,
                ToPort = "In"
            };
        }

        private static object FindOutput(InMemoryFlowEventSink sink, string nodeId, string outputName)
        {
            var variableName = nodeId + "." + outputName;
            var runtimeEvent = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), variableName, StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(runtimeEvent, "Expected output was not produced: " + variableName);
            return runtimeEvent.Data[FlowRuntimeDataKeys.Value];
        }
    }
}
