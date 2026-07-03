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
using Vision.Flow.Core.Runtime.Events;
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
    // 灏忓瀷娴嬭瘯鐢ㄤ緥鍖呰鍣紝渚涙帶鍒跺彴娴嬭瘯妗嗘灦澶嶇敤銆?
    internal sealed class TestCase
    {
        private readonly Func<Task> _runAsync;

        public TestCase(string name, Func<Task> runAsync)
        {
            Name = name;
            _runAsync = runAsync;
        }

        public string Name { get; private set; }

        public Task RunAsync()
        {
            return _runAsync();
        }
    }
}
