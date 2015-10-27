// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Core.Listener;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Core
{
    public class ErrorTriggerListenerTests
    {
        private readonly Mock<ITriggeredFunctionExecutor> _mockExecutor;

        public ErrorTriggerListenerTests()
        {
            _mockExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            _mockExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), CancellationToken.None))
                .Returns(Task.FromResult(new FunctionResult(true)));
        }

        [Fact]
        public void CreateTraceMonitor_SlidingWindow()
        {
            ParameterInfo parameter = typeof(Functions).GetMethod("SlidingWindowErrorHandler").GetParameters()[0];

            TraceMonitor traceMonitor = ErrorTriggerListener.CreateTraceMonitor(parameter, _mockExecutor.Object);

            SlidingWindowTraceFilter traceFilter = (SlidingWindowTraceFilter)traceMonitor.Filters.Single();
            Assert.Equal(5, traceFilter.Threshold);
            Assert.Equal("5 events at level 'Error' or lower have occurred within time window 00:05:00.", traceFilter.Message);
        }

        [Fact]
        public void CreateTraceMonitor_SlidingWindow_Customized()
        {
            ParameterInfo parameter = typeof(Functions).GetMethod("SlidingWindowErrorHandler_Customized").GetParameters()[0];

            TraceMonitor traceMonitor = ErrorTriggerListener.CreateTraceMonitor(parameter, _mockExecutor.Object);

            SlidingWindowTraceFilter traceFilter = (SlidingWindowTraceFilter)traceMonitor.Filters.Single();
            Assert.Equal(5, traceFilter.Threshold);
            Assert.Equal("Custom Message", traceFilter.Message);
        }

        [Fact]
        public void CreateTraceMonitor_AllErrors()
        {
            ParameterInfo parameter = typeof(Functions).GetMethod("AllErrors").GetParameters()[0];

            TraceMonitor traceMonitor = ErrorTriggerListener.CreateTraceMonitor(parameter, _mockExecutor.Object);

            TraceFilter.AnonymousTraceFilter traceFilter = (TraceFilter.AnonymousTraceFilter)traceMonitor.Filters.Single();
            Assert.Equal("One or more WebJob errors have occurred.", traceFilter.Message);

            int notification = 0;
            traceMonitor.Subscribe(p => notification++);

            Assert.Equal(0, traceFilter.Traces.Count);
            traceMonitor.Trace(new TraceEvent(TraceLevel.Error, "Error1"));
            Assert.Equal(1, traceFilter.Traces.Count);
            Assert.Equal("Error1", traceFilter.Traces.Single().Message);

            traceMonitor.Trace(new TraceEvent(TraceLevel.Error, "Error2"));
            Assert.Equal(1, traceFilter.Traces.Count);
            Assert.Equal("Error2", traceFilter.Traces.Single().Message);
            Assert.Equal(2, notification);
        }

        [Fact]
        public void CreateTraceMonitor_AllErrors_Customized()
        {
            ParameterInfo parameter = typeof(Functions).GetMethod("AllErrors_Customized").GetParameters()[0];

            TraceMonitor traceMonitor = ErrorTriggerListener.CreateTraceMonitor(parameter, _mockExecutor.Object);
            Assert.Equal(TimeSpan.Parse("00:30:00"), traceMonitor.NotificationThrottle);

            TraceFilter.AnonymousTraceFilter traceFilter = (TraceFilter.AnonymousTraceFilter)traceMonitor.Filters.Single();
            Assert.Equal("Custom Message", traceFilter.Message);

            int notification = 0;
            traceMonitor.Subscribe(p => notification++);

            Assert.Equal(0, traceFilter.Traces.Count);
            traceMonitor.Trace(new TraceEvent(TraceLevel.Error, "Error1"));
            Assert.Equal(1, traceFilter.Traces.Count);
            Assert.Equal("Error1", traceFilter.Traces.Single().Message);

            traceMonitor.Trace(new TraceEvent(TraceLevel.Error, "Error2"));
            Assert.Equal(1, traceFilter.Traces.Count);
            Assert.Equal("Error2", traceFilter.Traces.Single().Message);

            // expect second notification to be ignored due to throttle
            Assert.Equal(1, notification);
        }

        [Fact]
        public void CreateTraceMonitor_FunctionErrorHandler()
        {
            ParameterInfo parameter = typeof(Functions).GetMethod("TestErrorHandler").GetParameters()[0];

            TraceMonitor traceMonitor = ErrorTriggerListener.CreateTraceMonitor(parameter, _mockExecutor.Object);

            TraceFilter.AnonymousTraceFilter traceFilter = (TraceFilter.AnonymousTraceFilter)traceMonitor.Filters.Single();
            Assert.Equal("Function 'Functions.Test' failed.", traceFilter.Message);

            // first log a function exception for a *different* function
            // don't expect it to pass filter
            FunctionInvocationException functionException = new FunctionInvocationException("Function failed", new Exception("Kaboom!"))
            {
                MethodName = "Microsoft.Azure.WebJobs.Extensions.Tests.Core.ErrorTriggerListenerTests+Functions.Foo"
            };
            TraceEvent traceEvent = new TraceEvent(TraceLevel.Error, "Kaboom!", null, functionException);
            traceMonitor.Trace(traceEvent);
            Assert.Equal(0, traceFilter.Traces.Count);

            functionException = new FunctionInvocationException("Function failed", new Exception("Kaboom!"))
            {
                MethodName = "Microsoft.Azure.WebJobs.Extensions.Tests.Core.ErrorTriggerListenerTests+Functions.Test"
            };
            traceEvent = new TraceEvent(TraceLevel.Error, "Kaboom!", null, functionException);
            traceMonitor.Trace(traceEvent);
            Assert.Equal(1, traceFilter.Traces.Count);
            Assert.Same(functionException, traceFilter.Traces.Single().Exception);
        }

        [Fact]
        public void CreateTraceMonitor_CustomFilterType()
        {
            ParameterInfo parameter = typeof(Functions).GetMethod("CustomFilterType").GetParameters()[0];

            TraceMonitor traceMonitor = ErrorTriggerListener.CreateTraceMonitor(parameter, _mockExecutor.Object);
            Assert.Equal(TimeSpan.Parse("00:30:00"), traceMonitor.NotificationThrottle);

            Functions.CustomTraceFilter traceFilter = (Functions.CustomTraceFilter)traceMonitor.Filters.Single();
            Assert.NotNull(traceFilter);
        }

        [Fact]
        public void CreateTraceMonitor_FunctionErrorHandler_CustomFilterType()
        {
            ParameterInfo parameter = typeof(Functions).GetMethod("Test2ErrorHandler").GetParameters()[0];

            TraceMonitor traceMonitor = ErrorTriggerListener.CreateTraceMonitor(parameter, _mockExecutor.Object);

            ErrorTriggerListener.CompositeTraceFilter traceFilter = (ErrorTriggerListener.CompositeTraceFilter)traceMonitor.Filters.Single();
            Assert.NotNull(traceFilter);
            Assert.Equal(typeof(Functions.CustomTraceFilter), traceFilter.InnerTraceFilter.GetType());

            // first log a function exception for a *different* function
            // don't expect it to pass filter
            FunctionInvocationException functionException = new FunctionInvocationException("Function failed", new Exception("Kaboom!"))
            {
                MethodName = "Microsoft.Azure.WebJobs.Extensions.Tests.Core.ErrorTriggerListenerTests+Functions.Foo"
            };
            TraceEvent traceEvent = new TraceEvent(TraceLevel.Error, "Kaboom!", null, functionException);
            traceMonitor.Trace(traceEvent);
            Assert.Equal(0, traceFilter.Traces.Count);

            functionException = new FunctionInvocationException("Function failed", new Exception("Kaboom!"))
            {
                MethodName = "Microsoft.Azure.WebJobs.Extensions.Tests.Core.ErrorTriggerListenerTests+Functions.Test2"
            };
            traceEvent = new TraceEvent(TraceLevel.Error, "Kaboom!", null, functionException);
            traceMonitor.Trace(traceEvent);
            Assert.Equal(1, traceFilter.Traces.Count);
            Assert.Same(functionException, traceFilter.Traces.Single().Exception);
        }

        internal static class Functions
        {
            public static void Test()
            {
            }

            public static void TestErrorHandler([ErrorTrigger] TraceFilter filter)
            {
            }

            public static void Test2()
            {
            }

            public static void Test2ErrorHandler(
                [ErrorTrigger(typeof(CustomTraceFilter))] TraceFilter filter)
            {
            }

            public static void AllErrors([ErrorTrigger] TraceFilter filter)
            {
            }

            public static void AllErrors_Customized(
                [ErrorTrigger(Message = "Custom Message", Throttle = "00:30:00")] TraceFilter filter)
            {
            }

            public static void SlidingWindowErrorHandler([ErrorTrigger("00:05:00", 5)] TraceFilter filter)
            {
            }

            public static void SlidingWindowErrorHandler_Customized(
                [ErrorTrigger("00:05:00", 5, Message = "Custom Message", Throttle = "00:30:00")] TraceFilter filter)
            {
            }

            public static void CustomFilterType(
                [ErrorTrigger(typeof(CustomTraceFilter), Throttle = "00:30:00")] TraceFilter filter)
            {
            }

            internal class CustomTraceFilter : TraceFilter
            {
                private readonly Collection<TraceEvent> _traceEvents = new Collection<TraceEvent>();

                public override string Message
                {
                    get
                    {
                        return "Custom Error Filter";
                    }
                }

                public override Collection<TraceEvent> Traces
                {
                    get
                    {
                        return _traceEvents;
                    }
                }

                public override bool Filter(TraceEvent traceEvent)
                {
                    _traceEvents.Clear();
                    _traceEvents.Add(traceEvent);

                    return true;
                }
            }
        }
    }
}
