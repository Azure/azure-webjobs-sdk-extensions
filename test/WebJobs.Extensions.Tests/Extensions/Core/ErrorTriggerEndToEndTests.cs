// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Core
{
    public class ErrorTriggerEndToEndTests
    {
        [Fact]
        public void GlobalErrorHandler_SlidingWindow_InvokedAsExpected()
        {
            ErrorTriggerProgram_GlobalSlidingWindowHandler instance = new ErrorTriggerProgram_GlobalSlidingWindowHandler();

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            JobHost host = new JobHost(config);
            host.Start();

            MethodInfo method = instance.GetType().GetMethod("Throw");
            CallSafe(host, method);
            CallSafe(host, method);
            Assert.Null(instance.TraceFilter);

            CallSafe(host, method);
            Assert.NotNull(instance.TraceFilter);

            Assert.Equal("3 events at level 'Error' or lower have occurred within time window 00:05:00.", instance.TraceFilter.Message);
            Assert.Equal(3, instance.TraceFilter.Traces.Count);
            foreach (TraceEvent traceEvent in instance.TraceFilter.Traces)
            {
                FunctionInvocationException functionException = (FunctionInvocationException)traceEvent.Exception;
                Assert.Equal("Kaboom!", functionException.InnerException.Message);
                Assert.Equal("Microsoft.Azure.WebJobs.Extensions.Tests.Core.ErrorTriggerEndToEndTests+ErrorTriggerProgram_GlobalSlidingWindowHandler.Throw", functionException.MethodName);
            }
        }

        [Fact]
        public void GlobalErrorHandler_CatchAll_InvokedAsExpected()
        {
            ErrorTriggerProgram_GlobalCatchAllHandler instance = new ErrorTriggerProgram_GlobalCatchAllHandler();

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            JobHost host = new JobHost(config);
            host.Start();

            MethodInfo method = instance.GetType().GetMethod("Throw");
            CallSafe(host, method);
            Assert.NotNull(instance.TraceFilter);

            Assert.Equal("WebJob failure detected.", instance.TraceFilter.Message);
            Assert.Equal(1, instance.TraceFilter.Traces.Count);
        }

        [Fact]
        public void FunctionLevelErrorHandler_InvokedAsExpected()
        {
            ErrorTriggerProgram_FunctionLevelHandler instance = new ErrorTriggerProgram_FunctionLevelHandler();

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            JobHost host = new JobHost(config);
            host.Start();

            MethodInfo method = instance.GetType().GetMethod("ThrowA");
            CallSafe(host, method);
            CallSafe(host, method);
            Assert.Null(instance.TraceFilter);

            method = instance.GetType().GetMethod("ThrowB");
            CallSafe(host, method);

            Assert.Equal("Function 'ErrorTriggerProgram_FunctionLevelHandler.ThrowB' failed.", instance.TraceFilter.Message);
            Assert.Equal(1, instance.TraceFilter.Traces.Count);
        }

        [Fact]
        public void FunctionLevelErrorHandler_SlidingWindow_InvokedAsExpected()
        {
            ErrorTriggerProgram_FunctionLevelHandler_SlidingWindow instance = new ErrorTriggerProgram_FunctionLevelHandler_SlidingWindow();

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            JobHost host = new JobHost(config);
            host.Start();

            MethodInfo method = instance.GetType().GetMethod("Throw");
            CallSafe(host, method);
            CallSafe(host, method);
            Assert.Null(instance.TraceFilter);

            CallSafe(host, method);

            Assert.Equal(3, instance.TraceFilter.Traces.Count);
            Assert.Equal("3 events at level 'Error' or lower have occurred within time window 00:10:00.", instance.TraceFilter.Message);
            Assert.True(instance.TraceFilter.Traces.All(p => p.Message == "Exception while executing function: ErrorTriggerProgram_FunctionLevelHandler_SlidingWindow.Throw"));
        }

        [Fact]
        public void GlobalErrorHandler_HandlerFails_NoInfiniteLoop()
        {
            ErrorTriggerProgram_GlobalCatchAllHandler instance = new ErrorTriggerProgram_GlobalCatchAllHandler(fail: true);

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            JobHost host = new JobHost(config);
            host.Start();

            TestTraceWriter traceWriter = new TestTraceWriter();
            config.Tracing.Tracers.Add(traceWriter);

            MethodInfo method = instance.GetType().GetMethod("Throw");
            CallSafe(host, method);

            Assert.Equal(1, instance.Errors.Count);
            TraceEvent error = instance.Errors.Single();
            Assert.Equal("Exception while executing function: ErrorTriggerProgram_GlobalCatchAllHandler.Throw", error.Message);

            // make sure the error handler failure is still logged
            var traces = traceWriter.Traces;
            Assert.Equal(8, traces.Count);
            Assert.True(traces[0].Message.StartsWith("Executing: 'ErrorTriggerProgram_GlobalCatchAllHandler.Throw'"));
            Assert.True(traces[1].Message.StartsWith("Executing: 'ErrorTriggerProgram_GlobalCatchAllHandler.ErrorHandler'"));
            Assert.True(traces[2].Message.StartsWith("Exception while executing function: ErrorTriggerProgram_GlobalCatchAllHandler.ErrorHandler"));
            Assert.Equal("Kaboom!", traces[3].Exception.InnerException.Message);
            Assert.True(traces[3].Message.StartsWith("Executed: 'ErrorTriggerProgram_GlobalCatchAllHandler.ErrorHandler' (Failed)"));
            Assert.True(traces[4].Message.StartsWith("  Function had errors. See Azure WebJobs SDK dashboard for details."));
            Assert.True(traces[5].Message.StartsWith("Exception while executing function: ErrorTriggerProgram_GlobalCatchAllHandler.Throw"));
            Assert.True(traces[6].Message.StartsWith("Executed: 'ErrorTriggerProgram_GlobalCatchAllHandler.Throw' (Failed)"));
            Assert.True(traces[7].Message.StartsWith("  Function had errors. See Azure WebJobs SDK dashboard for details."));
        }

        [Fact]
        public void GlobalErrorHandler_ManualSubscriberFails_NoInfiniteLoop()
        {
            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(typeof(ErrorProgram))
            };
            config.UseCore();

            int notificationCount = 0;
            var traceMonitor = new TraceMonitor()
                .Filter(p => { return true; })
                .Subscribe(p =>
                {
                    notificationCount++;
                    throw new Exception("Kaboom");
                });
            config.Tracing.Tracers.Add(traceMonitor);

            JobHost host = new JobHost(config);
            host.Start();

            TestTraceWriter traceWriter = new TestTraceWriter();
            config.Tracing.Tracers.Add(traceWriter);

            MethodInfo method = typeof(ErrorProgram).GetMethod("Throw");
            CallSafe(host, method);

            Assert.Equal(1, notificationCount);

            var traces = traceWriter.Traces;
            Assert.Equal(4, traces.Count);
            Assert.True(traces[0].Message.StartsWith("Executing: 'ErrorProgram.Throw'"));
            Assert.True(traces[1].Message.StartsWith("Exception while executing function: ErrorProgram.Throw"));
            Assert.True(traces[2].Message.StartsWith("Executed: 'ErrorProgram.Throw' (Failed)"));
            Assert.True(traces[3].Message.StartsWith("  Function had errors. See Azure WebJobs SDK dashboard for details."));
        }

        private void CallSafe(JobHost host, MethodInfo method)
        {
            try
            {
                host.Call(method);
            }
            catch
            {
            }
        }

        public static class ErrorProgram
        {
            [NoAutomaticTrigger]
            public static void Throw()
            {
                throw new Exception("Kaboom!");
            }
        }

        public class ErrorTriggerProgram_GlobalSlidingWindowHandler
        {
            public TraceFilter TraceFilter { get; set; }

            [NoAutomaticTrigger]
            public void Throw()
            {
                throw new Exception("Kaboom!");
            }

            public void ErrorHandler([ErrorTrigger("00:05:00", 3)] TraceFilter filter)
            {
                TraceFilter = filter;
            }
        }

        public class ErrorTriggerProgram_GlobalCatchAllHandler
        {
            private readonly bool _fail;

            public ErrorTriggerProgram_GlobalCatchAllHandler(bool fail = false)
            {
                _fail = fail;
                Errors = new Collection<TraceEvent>();
            }

            public TraceFilter TraceFilter { get; set; }

            public Collection<TraceEvent> Errors { get; private set; } 

            [NoAutomaticTrigger]
            public void Throw()
            {
                throw new Exception("Kaboom!");
            }

            public void ErrorHandler([ErrorTrigger] TraceFilter traceFilter)
            {
                TraceFilter = traceFilter;

                foreach (TraceEvent error in traceFilter.Traces)
                {
                    Errors.Add(error);
                }

                if (_fail)
                {
                    throw new Exception("Kaboom!");
                }
            }
        }

        public class ErrorTriggerProgram_FunctionLevelHandler
        {
            public TraceFilter TraceFilter { get; set; }

            [NoAutomaticTrigger]
            public void ThrowA()
            {
                throw new Exception("Kaboom!");
            }

            [NoAutomaticTrigger]
            public void ThrowB()
            {
                throw new Exception("Kaboom!");
            }

            public void ThrowBErrorHandler([ErrorTrigger] TraceFilter filter)
            {
                TraceFilter = filter;
            }
        }

        public class ErrorTriggerProgram_FunctionLevelHandler_SlidingWindow
        {
            public TraceFilter TraceFilter { get; set; }

            [NoAutomaticTrigger]
            public void Throw()
            {
                throw new Exception("Kaboom!");
            }

            public void ThrowErrorHandler(
                [ErrorTrigger("00:10:00", 3)] TraceFilter filter)
            {
                TraceFilter = filter;
            }
        }

        public class ExplicitJobActivator : IJobActivator
        {
            private readonly object _instance;

            public ExplicitJobActivator(object instance)
            {
                _instance = instance;
            }

            public T CreateInstance<T>()
            {
                return (T)_instance;
            }
        }
    }
}
