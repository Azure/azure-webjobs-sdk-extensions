// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers.Scheduling;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Bindings;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers.Bindings
{
    public class TimerTriggerBindingTests
    {
        private readonly ILogger _logger;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly string _timerName;
        private readonly Mock<ScheduleMonitor> _mockScheduleMonitor;
        private readonly TimerTriggerBinding _binding;
        private readonly TimerSchedule _schedule;

        public TimerTriggerBindingTests()
        {
            _loggerProvider = new TestLoggerProvider();
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);
            _logger = _loggerProvider.CreateLogger("Test");

            ParameterInfo parameter = GetType().GetMethod("TestTimerJob").GetParameters()[0];
            MethodInfo methodInfo = (MethodInfo)parameter.Member;
            _timerName = string.Format("{0}.{1}", methodInfo.DeclaringType.FullName, methodInfo.Name);

            _mockScheduleMonitor = new Mock<ScheduleMonitor>(MockBehavior.Strict);

            TimerTriggerAttribute attribute = parameter.GetCustomAttribute<TimerTriggerAttribute>();
            INameResolver nameResolver = new TestNameResolver();
            _schedule = TimerSchedule.Create(attribute, nameResolver, _logger);
            TimersOptions options = new TimersOptions();

            Mock<IDrainModeManager> mockDrainModeManager = new Mock<IDrainModeManager>(MockBehavior.Strict);

            _binding = new TimerTriggerBinding(parameter, attribute, _schedule, options, loggerFactory.CreateLogger("Test"), _mockScheduleMonitor.Object, mockDrainModeManager.Object);
        }

        [Fact]
        public async Task BindAsync_ReturnsExpectedTriggerData()
        {
            ScheduleStatus status = new ScheduleStatus();
            _mockScheduleMonitor.Setup(p => p.GetStatusAsync(_timerName, It.IsAny<CancellationToken>())).ReturnsAsync(status);

            // when we bind to a non-TimerInfo (e.g. in a Dashboard invocation) a new
            // TimerInfo is created, with the ScheduleStatus populated
            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            TriggerData triggerData = (TriggerData)(await _binding.BindAsync(string.Empty, context));
            TimerInfo timerInfo = (TimerInfo)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Same(status, timerInfo.ScheduleStatus);

            // when we pass in a TimerInfo that is used
            TimerInfo expected = new TimerInfo(_schedule, status);
            triggerData = (TriggerData)(await _binding.BindAsync(expected, context));
            timerInfo = (TimerInfo)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Same(expected, timerInfo);
        }

        [Fact]
        public Task BindAsync_InvalidStatus_DefaultsToValidDates_Tokyo()
        {
            using var tz = TimeZoneSetter.TokyoStandard;
            return BindAsync_InvalidStatus_DefaultsToValidDates();
        }

        [Fact]
        public Task BindAsync_InvalidStatus_DefaultsToValidDates_Pacific()
        {
            using var tz = TimeZoneSetter.PacificStandard;
            return BindAsync_InvalidStatus_DefaultsToValidDates();
        }

        [Fact]
        public Task BindAsync_InvalidStatus_DefaultsToValidDates_Utc()
        {
            using var tz = TimeZoneSetter.Utc;
            return BindAsync_InvalidStatus_DefaultsToValidDates();
        }

        private async Task BindAsync_InvalidStatus_DefaultsToValidDates()
        {
            var testStart = DateTime.Now;

            // This is invalid in UTC + time zones like Tokyo but used to be used as a 
            // default value in the past.
            var invalidDateTime = new DateTime(0, DateTimeKind.Local);

            var invalidStatus = new ScheduleStatus()
            {
                Last = invalidDateTime,
                Next = ScheduleMonitor.DefaultDateTimeThreshold.AddMinutes(-1),
                LastUpdated = DateTime.MinValue
            };

            _mockScheduleMonitor
                .Setup(p => p.GetStatusAsync(_timerName, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(invalidStatus));

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);

            TriggerData triggerData = (TriggerData)(await _binding.BindAsync(string.Empty, context));
            TimerInfo timerInfo = (TimerInfo)(await triggerData.ValueProvider.GetValueAsync());

            ScheduleMonitorTests.ValidateSchedule(timerInfo.ScheduleStatus);

            // Validate that all were set to default
            Assert.Equal(ScheduleMonitor.DefaultDateTime, timerInfo.ScheduleStatus.Last);
            Assert.Equal(ScheduleMonitor.DefaultDateTime, timerInfo.ScheduleStatus.Next);
            Assert.Equal(ScheduleMonitor.DefaultDateTime, timerInfo.ScheduleStatus.LastUpdated);
        }

        public static void TestTimerJob([TimerTrigger("5:00:00")] TimerInfo timer)
        {
        }
    }
}
