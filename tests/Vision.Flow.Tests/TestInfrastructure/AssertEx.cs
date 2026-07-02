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
    // 最小断言工具让控制台测试框架保持无外部依赖。
    internal static class AssertEx
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void False(bool condition, string message)
        {
            True(!condition, message);
        }

        public static void NotNull(object value, string message)
        {
            True(value != null, message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + ", Actual: " + actual);
            }
        }

        public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
        {
            var expectedList = expected.ToList();
            var actualList = actual.ToList();
            if (expectedList.Count != actualList.Count)
            {
                throw new InvalidOperationException(message + " Expected count: " + expectedList.Count + ", Actual count: " + actualList.Count + ". Actual: " + string.Join(", ", actualList));
            }

            for (var index = 0; index < expectedList.Count; index++)
            {
                if (!object.Equals(expectedList[index], actualList[index]))
                {
                    throw new InvalidOperationException(message + " Difference at index " + index + ". Expected: " + expectedList[index] + ", Actual: " + actualList[index]);
                }
            }
        }

        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException ex)
            {
                return ex;
            }

            throw new InvalidOperationException("Expected exception was not thrown: " + typeof(TException).FullName);
        }
    }
}
