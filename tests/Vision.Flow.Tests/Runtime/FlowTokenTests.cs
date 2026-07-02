using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Nodes;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Tests
{
    // 运行时令牌测试聚焦流程执行共享的变量存取行为。
    internal static class FlowTokenTests
    {
        public static Task SetGetTryGet()
        {
            var token = new FlowToken
            {
                ProductId = "P-001",
                WorkpieceId = "W-001"
            };

            token.Set("Score", 98);
            token.Set("Name", "part-a");
            token.Metadata["Line"] = "L1";

            AssertEx.Equal("P-001", token.ProductId, "ProductId should be stored.");
            AssertEx.Equal(98, token.Get<int>("Score"), "Integer value should round-trip.");
            AssertEx.Equal("part-a", token.Get<string>("Name"), "String value should round-trip.");
            AssertEx.Equal("L1", Convert.ToString(token.Metadata["Line"]), "Metadata value should round-trip.");

            int score;
            AssertEx.True(token.TryGet<int>("Score", out score), "TryGet should find Score.");
            AssertEx.Equal(98, score, "TryGet should return the converted Score.");

            object missing;
            AssertEx.False(token.TryGet("Missing", out missing), "TryGet should return false for missing keys.");
            return Task.FromResult(0);
        }
    }
}
