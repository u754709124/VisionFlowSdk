using System;
using System.Globalization;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Nodes
{
    internal static class ControlFlowNodeHelpers
    {
        public static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = ResolveObject(context, name, defaultValue);
            return value == null ? defaultValue : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static int ResolveInt32(FlowExecutionContext context, string name, int defaultValue)
        {
            var value = ResolveObject(context, name, defaultValue);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static object ResolveObject(FlowExecutionContext context, string name, object defaultValue)
        {
            var value = context.GetSettingValue(name);
            return value ?? defaultValue;
        }
    }
}
