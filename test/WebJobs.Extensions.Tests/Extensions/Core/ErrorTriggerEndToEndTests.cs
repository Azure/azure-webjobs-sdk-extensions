// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Core
{
    [Trait("Category", "E2E")]
    public class ErrorTriggerEndToEndTests
    {
        [Fact]
        public async Task GlobalErrorHandler_SlidingWindow_InvokedAsExpected()
        {
            ErrorTriggerProgram_GlobalSlidingWindowHandler instance = new ErrorTriggerProgram_GlobalSlidingWindowHandler();

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            config.AddService<IWebJobsExceptionHandler>(new TestExceptionHandler());
            JobHost host = new JobHost(config);
            await host.StartAsync();

            MethodInfo method = instance.GetType().GetMethod("Throw");
            await CallSafe(host, method);
            await CallSafe(host, method);
            Assert.Null(instance.TraceFilter);

            await CallSafe(host, method);
            Assert.NotNull(instance.TraceFilter);

            IEnumerable<TraceEvent> events = instance.TraceFilter.GetEvents();
            Assert.Equal("3 events at level 'Error' or lower have occurred within time window 00:05:00.", instance.TraceFilter.Message);
            Assert.Equal(3, events.Count());
            foreach (TraceEvent traceEvent in events)
            {
                FunctionInvocationException functionException = (FunctionInvocationException)traceEvent.Exception;
                Assert.Equal("Kaboom!", functionException.InnerException.Message);
                Assert.Equal("Microsoft.Azure.WebJobs.Extensions.Tests.Core.ErrorTriggerEndToEndTests+ErrorTriggerProgram_GlobalSlidingWindowHandler.Throw", functionException.MethodName);
            }
        }

        [Fact]
        public async Task GlobalErrorHandler_CatchAll_InvokedAsExpected()
        {
            ErrorTriggerProgram_GlobalCatchAllHandler instance = new ErrorTriggerProgram_GlobalCatchAllHandler();

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            config.AddService<IWebJobsExceptionHandler>(new TestExceptionHandler());
            JobHost host = new JobHost(config);
            await host.StartAsync();

            MethodInfo method = instance.GetType().GetMethod("Throw");
            await CallSafe(host, method);
            Assert.NotNull(instance.TraceFilter);

            Assert.Equal("One or more WebJob errors have occurred.", instance.TraceFilter.Message);
            Assert.Equal(1, instance.TraceFilter.GetEvents().Count());
        }

        [Fact]
        public async Task FunctionLevelErrorHandler_InvokedAsExpected()
        {
            ErrorTriggerProgram_FunctionLevelHandler instance = new ErrorTriggerProgram_FunctionLevelHandler();

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            config.AddService<IWebJobsExceptionHandler>(new TestExceptionHandler());
            JobHost host = new JobHost(config);
            await host.StartAsync();

            MethodInfo method = instance.GetType().GetMethod("ThrowA");
            await CallSafe(host, method);
            await CallSafe(host, method);
            Assert.Null(instance.TraceFilter);

            method = instance.GetType().GetMethod("ThrowB");
            await CallSafe(host, method);

            Assert.Equal("Function 'ErrorTriggerProgram_FunctionLevelHandler.ThrowB' failed.", instance.TraceFilter.Message);
            Assert.Equal(1, instance.TraceFilter.GetEvents().Count());
        }

        [Fact]
        public async Task FunctionLevelErrorHandler_SlidingWindow_InvokedAsExpected()
        {
            ErrorTriggerProgram_FunctionLevelHandler_SlidingWindow instance = new ErrorTriggerProgram_FunctionLevelHandler_SlidingWindow();

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            config.AddService<IWebJobsExceptionHandler>(new TestExceptionHandler());
            JobHost host = new JobHost(config);
            await host.StartAsync();

            MethodInfo method = instance.GetType().GetMethod("Throw");
            await CallSafe(host, method);
            await CallSafe(host, method);
            Assert.Null(instance.TraceFilter);

            await CallSafe(host, method);

            IEnumerable<TraceEvent> events = instance.TraceFilter.GetEvents();
            Assert.Equal(3, events.Count());
            Assert.Equal("3 events at level 'Error' or lower have occurred within time window 00:10:00.", instance.TraceFilter.Message);
            Assert.True(events.All(p => p.Message == "Exception while executing function: ErrorTriggerProgram_FunctionLevelHandler_SlidingWindow.Throw"));
        }

        [Fact]
        public async Task GlobalErrorHandler_HandlerFails_NoInfiniteLoop()
        {
            ErrorTriggerProgram_GlobalCatchAllHandler instance = new ErrorTriggerProgram_GlobalCatchAllHandler(fail: true);

            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(instance.GetType()),
                JobActivator = new ExplicitJobActivator(instance)
            };
            config.UseCore();
            config.AddService<IWebJobsExceptionHandler>(new TestExceptionHandler());
            JobHost host = new JobHost(config);
            await host.StartAsync();

            TestTraceWriter traceWriter = new TestTraceWriter();
            config.Tracing.Tracers.Add(traceWriter);

            MethodInfo method = instance.GetType().GetMethod("Throw");
            await CallSafe(host, method);

            Assert.Equal(1, instance.Errors.Count);
            TraceEvent error = instance.Errors.Single();
            Assert.Equal("Exception while executing function: ErrorTriggerProgram_GlobalCatchAllHandler.Throw", error.Message);

            // make sure the error handler failure is still logged
            var events = traceWriter.Events;
            Assert.Equal(8, events.Count);
            Assert.StartsWith("Executing 'ErrorTriggerProgram_GlobalCatchAllHandler.Throw'", events[0].Message);
            Assert.StartsWith("Executing 'ErrorTriggerProgram_GlobalCatchAllHandler.ErrorHandler'", events[1].Message);
            Assert.StartsWith("Exception while executing function: ErrorTriggerProgram_GlobalCatchAllHandler.ErrorHandler", events[2].Message);
            Assert.Equal("Kaboom!", events[3].Exception.InnerException.Message);
            Assert.StartsWith("Executed 'ErrorTriggerProgram_GlobalCatchAllHandler.ErrorHandler' (Failed, ", events[3].Message);
            Assert.StartsWith("  Function had errors. See Azure WebJobs SDK dashboard for details.", events[4].Message);
            Assert.StartsWith("Exception while executing function: ErrorTriggerProgram_GlobalCatchAllHandler.Throw", events[5].Message);
            Assert.StartsWith("Executed 'ErrorTriggerProgram_GlobalCatchAllHandler.Throw' (Failed, ", events[6].Message);
            Assert.StartsWith("  Function had errors. See Azure WebJobs SDK dashboard for details.", events[7].Message);
        }

        [Fact]
        public async Task GlobalErrorHandler_ManualSubscriberFails_NoInfiniteLoop()
        {
            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new ExplicitTypeLocator(typeof(ErrorProgram))
            };
            config.UseCore();
            config.AddService<IWebJobsExceptionHandler>(new TestExceptionHandler());
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
            await host.StartAsync();

            TestTraceWriter traceWriter = new TestTraceWriter();
            config.Tracing.Tracers.Add(traceWriter);

            MethodInfo method = typeof(ErrorProgram).GetMethod("Throw");
            await CallSafe(host, method);

            Assert.Equal(1, notificationCount);

            var events = traceWriter.Events;

            Assert.Equal(4, events.Count);
            Assert.StartsWith("Executing 'ErrorProgram.Throw'", events[0].Message);
            Assert.StartsWith("Exception while executing function: ErrorProgram.Throw", events[1].Message);
            Assert.StartsWith("Executed 'ErrorProgram.Throw' (Failed, ", events[2].Message);
            Assert.StartsWith("  Function had errors. See Azure WebJobs SDK dashboard for details.", events[3].Message);
        }

        private async Task CallSafe(JobHost host, MethodInfo method)
        {
            try
            {
                await host.CallAsync(method);
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

                foreach (TraceEvent error in traceFilter.GetEvents())
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
