// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers
{
    public class TimerListenerTests
    {
        private string _testTimerName = "Program.TestTimerJob";
        private TimerListener _listener;
        private Mock<ScheduleMonitor> _mockScheduleMonitor;
        private TimersConfiguration _config;
        private TimerTriggerAttribute _attribute;
        private Mock<ITriggeredFunctionExecutor> _mockTriggerExecutor;
        private TriggeredFunctionData _triggeredFunctionData;

        public TimerListenerTests()
        {
            CreateTestListener("0 */1 * * * *");
        }

        [Fact]
        public async Task InvokeJobFunction_UpdatesScheduleMonitor()
        {
            DateTime lastOccurrence = DateTime.Now;
            DateTime nextOccurrence = _attribute.Schedule.GetNextOccurrence(lastOccurrence);

            ScheduleStatus status = new ScheduleStatus
            {
                Last = lastOccurrence,
                Next = nextOccurrence
            };
            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName, 
                It.Is<ScheduleStatus>(q => q.Last == lastOccurrence && q.Next == nextOccurrence)))
                .Returns(Task.FromResult(true));

            await _listener.InvokeJobFunction(lastOccurrence, false);

            _listener.Dispose();
        }

        [Fact]
        public async Task InvokeJobFunction_UseMonitorFalse_DoesNotUpdateScheduleMonitor()
        {
            _attribute.UseMonitor = false;

            await _listener.InvokeJobFunction(DateTime.Now, false);

            _listener.Dispose();
        }

        [Fact]
        public async Task InvokeJobFunction_HandlesExceptions()
        {
            _attribute.UseMonitor = false;
            _mockTriggerExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>())).Throws(new Exception("Kaboom!"));

            await _listener.InvokeJobFunction(DateTime.Now, false);

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_SchedulePastDue_InvokesJobFunctionImmediately()
        {
            // Set this to true to ensure that the function is only executed once
            // In this case, because it is run on startup due to being behind schedule,
            // it shouldn't be run twice.
            _attribute.RunOnStartup = true;

            DateTime lastOccurrence = default(DateTime);

            _mockScheduleMonitor.Setup(p => p.IsPastDueAsync(_testTimerName, It.IsAny<DateTime>(), It.IsAny<TimerSchedule>()))
                .Callback<string, DateTime, TimerSchedule>((mockTimerName, mockNow, mockNext) =>
                    {
                        lastOccurrence = mockNow;
                    })
                .Returns(Task.FromResult(true));

            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName, It.IsAny<ScheduleStatus>()))
                .Callback<string, ScheduleStatus>((mockTimerName, mockStatus) =>
                    {
                        Assert.Equal(lastOccurrence, mockStatus.Last);
                        DateTime expectedNextOccurrence = _attribute.Schedule.GetNextOccurrence(lastOccurrence);
                        Assert.Equal(expectedNextOccurrence, mockStatus.Next);
                    })
                .Returns(Task.FromResult(true));

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            TimerInfo timerInfo = (TimerInfo)_triggeredFunctionData.TriggerValue;
            Assert.True(timerInfo.IsPastDue);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Once());

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_ScheduleNotPastDue_DoesNotInvokeJobFunctionImmediately()
        {
            _mockScheduleMonitor.Setup(p => p.IsPastDueAsync(_testTimerName, It.IsAny<DateTime>(), It.IsAny<TimerSchedule>()))
                .Returns(Task.FromResult(false));

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Never());

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_RunOnStartup_InvokesJobFunctionImmediately()
        {
            _attribute.UseMonitor = false;
            _attribute.RunOnStartup = true;

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Once());

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_UseMonitorFalse_DoesNotCheckForPastDueSchedule()
        {
            _attribute.UseMonitor = false;

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_ExtendedScheduleInterval_TimerContinuesUntilTotalIntervalComplete()
        {
            // create a timer with an extended interval that exceeds the max
            TimeSpan interval = TimerListener.MaxTimerInterval + TimerListener.MaxTimerInterval + TimeSpan.FromDays(4);
            CreateTestListener(interval.ToString(), useMonitor: false);

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);
            Assert.Equal(TimerListener.MaxTimerInterval.TotalMilliseconds, _listener.Timer.Interval);

            // simulate first timer event - expect the timer to continue without
            // invoking the job function
            await _listener.HandleTimerEvent();
            Assert.Equal(TimerListener.MaxTimerInterval.TotalMilliseconds, _listener.Timer.Interval);

            // simulate second timer event - expect the timer to continue without
            // invoking the job function
            await _listener.HandleTimerEvent();
            Assert.Equal(TimeSpan.FromDays(4).TotalMilliseconds, _listener.Timer.Interval);

            // simulate final timer event for the interval - expect the job function to be executed now,
            // and the interval start from the beginning
            await _listener.HandleTimerEvent();
            Assert.Equal(TimerListener.MaxTimerInterval.TotalMilliseconds, _listener.Timer.Interval);

            // verify that the job function was only invoked once
            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Once());

            _listener.Dispose();
        }

        [Fact]
        public void Timer_VerifyMaxInterval()
        {
            // verify that the maximum interval works
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = TimerListener.MaxTimerInterval.TotalMilliseconds;
            timer.Start();

            // exceed the max - expect an exception
            timer.Stop();
            timer.Interval = (TimerListener.MaxTimerInterval + TimeSpan.FromDays(1)).TotalMilliseconds;
            Assert.Throws<ArgumentOutOfRangeException>(() => timer.Start());
        }

        private void CreateTestListener(string expression, bool useMonitor = true)
        {
            _attribute = new TimerTriggerAttribute(expression);
            _attribute.UseMonitor = useMonitor;
            _config = new TimersConfiguration();
            _mockScheduleMonitor = new Mock<ScheduleMonitor>(MockBehavior.Strict);
            _config.ScheduleMonitor = _mockScheduleMonitor.Object;
            _mockTriggerExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            FunctionResult result = new FunctionResult(true);
            _mockTriggerExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Callback<TriggeredFunctionData, CancellationToken>((mockFunctionData, mockToken) =>
                {
                    _triggeredFunctionData = mockFunctionData;
                })
                .Returns(Task.FromResult(result));
            JobHostConfiguration hostConfig = new JobHostConfiguration();
            hostConfig.HostId = "testhostid";
            _listener = new TimerListener(_attribute, _testTimerName, _config, _mockTriggerExecutor.Object, new TestTraceWriter());
        }
    }
}
