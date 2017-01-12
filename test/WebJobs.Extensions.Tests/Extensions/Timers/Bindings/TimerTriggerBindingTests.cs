// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers.Bindings
{
    public class TimerTriggerBindingTests
    {
        [Fact]
        public async Task BindAsync_ReturnsExpectedTriggerData()
        {
            ParameterInfo parameter = GetType().GetMethod("TestTimerJob").GetParameters()[0];
            MethodInfo methodInfo = (MethodInfo)parameter.Member;
            string timerName = string.Format("{0}.{1}", methodInfo.DeclaringType.FullName, methodInfo.Name);

            Mock<ScheduleMonitor> mockScheduleMonitor = new Mock<ScheduleMonitor>(MockBehavior.Strict);
            ScheduleStatus status = new ScheduleStatus();
            mockScheduleMonitor.Setup(p => p.GetStatusAsync(timerName)).ReturnsAsync(status);

            TimerTriggerAttribute attribute = parameter.GetCustomAttribute<TimerTriggerAttribute>();
            INameResolver nameResolver = new TestNameResolver();
            TimerSchedule schedule = TimerSchedule.Create(attribute, nameResolver);
            TimersConfiguration config = new TimersConfiguration();
            config.ScheduleMonitor = mockScheduleMonitor.Object;
            TestTraceWriter trace = new TestTraceWriter();
            TimerTriggerBinding binding = new TimerTriggerBinding(parameter, attribute, schedule, config, trace);

            // when we bind to a non-TimerInfo (e.g. in a Dashboard invocation) a new
            // TimerInfo is created, with the ScheduleStatus populated
            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None, trace);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            TriggerData triggerData = (TriggerData)(await binding.BindAsync(string.Empty, context));
            TimerInfo timerInfo = (TimerInfo)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Same(status, timerInfo.ScheduleStatus);

            // when we pass in a TimerInfo that is used
            TimerInfo expected = new TimerInfo(schedule, status);
            triggerData = (TriggerData)(await binding.BindAsync(expected, context));
            timerInfo = (TimerInfo)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Same(expected, timerInfo);
        }

        public static void TestTimerJob([TimerTrigger("5:00:00")] TimerInfo timer)
        {
        }
    }
}
