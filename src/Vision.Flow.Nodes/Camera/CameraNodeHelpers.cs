using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // 共享相机节点辅助方法保持相机节点间的绑定和标量解析一致。
    internal static class CameraNodeHelpers
    {
        public static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = context.GetInputValue(name);
            return value == null ? defaultValue : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static int ResolveInt32(FlowExecutionContext context, string name, int defaultValue)
        {
            var value = context.GetInputValue(name);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static bool ResolveBoolean(FlowExecutionContext context, string name, bool defaultValue)
        {
            var value = context.GetInputValue(name);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
    }
}
