// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Moq;
using NCrontab;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers
{
    public class TimerListenerTests
    {
        private readonly string _testTimerName = "Program.TestTimerJob";
        private TimerListener _listener;
        private Mock<ScheduleMonitor> _mockScheduleMonitor;
        private TimersConfiguration _config;
        private TimerTriggerAttribute _attribute;
        private TimerSchedule _schedule;
        private Mock<ITriggeredFunctionExecutor> _mockTriggerExecutor;
        private TriggeredFunctionData _triggeredFunctionData;
        private TestTraceWriter _traceWriter;

        public TimerListenerTests()
        {
            CreateTestListener("0 */1 * * * *");
        }

        [Fact]
        public async Task InvokeJobFunction_UpdatesScheduleMonitor()
        {
            DateTime lastOccurrence = DateTime.Now;
            DateTime nextOccurrence = _schedule.GetNextOccurrence(lastOccurrence);

            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName,
                It.Is<ScheduleStatus>(q => q.Last == lastOccurrence && q.Next == nextOccurrence)))
                .Returns(Task.FromResult(true));

            await _listener.InvokeJobFunction(lastOccurrence, false);

            _listener.Dispose();
        }

        [Theory]
        [InlineData("0 0 0 * * *", true)]
        [InlineData("0 0 0 * * *", false)]
        [InlineData("1.00:00:00", true)]
        public async Task InvokeJobFunction_UpdatesScheduleMonitor_AccountsForSkew(string schedule, bool useMonitor)
        {
            CreateTestListener(schedule, useMonitor);

            var status = new ScheduleStatus
            {
                Last = new DateTime(2016, 3, 4),
                Next = new DateTime(2016, 3, 5)
            };

            // Run the function 1 millisecond before it's next scheduled run.
            DateTime invocationTime = status.Next.AddMilliseconds(-1);

            // It should not use the same 'Next' value twice in a row.
            DateTime expectedNextOccurrence = new DateTime(2016, 3, 6);

            bool monitorCalled = false;
            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName,
                It.Is<ScheduleStatus>(q => q.Last == status.Next && q.Next == expectedNextOccurrence)))
                .Callback(() => monitorCalled = true)
                .Returns(Task.FromResult(true));

            // Initialize the _scheduleStatus
            _listener.ScheduleStatus = status;

            await _listener.InvokeJobFunction(invocationTime, isPastDue: false, runOnStartup: false);

            _listener.Dispose();

            Assert.Equal(status.Next, _listener.ScheduleStatus.Last);
            Assert.Equal(expectedNextOccurrence, _listener.ScheduleStatus.Next);
            Assert.Equal(monitorCalled, useMonitor);
        }

        [Fact]
        public async Task InvokeJobFunction_UseMonitorFalse_DoesNotUpdateScheduleMonitor()
        {
            _listener.ScheduleMonitor = null;

            await _listener.InvokeJobFunction(DateTime.Now, false);

            _listener.Dispose();
        }

        [Fact]
        public async Task InvokeJobFunction_HandlesExceptions()
        {
            _listener.ScheduleMonitor = null;
            _mockTriggerExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>())).Throws(new Exception("Kaboom!"));

            await _listener.InvokeJobFunction(DateTime.Now, false);

            _listener.Dispose();
        }

        [Fact]
        public async Task ClockSkew_IsNotCalculatedPastDue()
        {
            // First, invoke a function with clock skew. This will store the next status back in the 
            // 'updatedStatus' variable.
            CreateTestListener("0 0 0 * * *");
            var status = new ScheduleStatus
            {
                Last = new DateTime(2016, 3, 4),
                Next = new DateTime(2016, 3, 5),
                LastUpdated = new DateTime(2016, 3, 4)
            };
            DateTime invocationTime = status.Next.AddMilliseconds(-1);
            ScheduleStatus updatedStatus = null;
            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName, It.IsAny<ScheduleStatus>()))
                .Callback<string, ScheduleStatus>((n, s) => updatedStatus = s)
                .Returns(Task.FromResult(true));
            _listener.ScheduleStatus = status;
            await _listener.InvokeJobFunction(invocationTime, isPastDue: false, runOnStartup: false);
            _listener.Dispose();

            // Now, use that status variable to calculate past due (this ultimately calls the base class implementation).
            // This ensures we do not consider clock skewed functions as past due -- this was previously a bug.
            // Use a new mock monitor so we can CallBase on it without affecting the class-level one.
            var mockMonitor = new Mock<ScheduleMonitor>();
            mockMonitor.CallBase = true;
            DateTime hostStartTime = new DateTime(2016, 3, 5, 1, 0, 0);
            TimeSpan pastDue = await mockMonitor.Object.CheckPastDueAsync(_testTimerName, hostStartTime, _schedule, updatedStatus);

            Assert.Equal(TimeSpan.Zero, pastDue);
            _mockScheduleMonitor.VerifyAll();
        }

        [Fact]
        public async Task StartAsync_SchedulePastDue_InvokesJobFunctionImmediately()
        {
            // Set this to true to ensure that the function is only executed once
            // In this case, because it is run on startup due to being behind schedule,
            // it shouldn't be run twice.
            _attribute.RunOnStartup = true;

            ScheduleStatus status = new ScheduleStatus();
            _mockScheduleMonitor.Setup(p => p.GetStatusAsync(_testTimerName)).ReturnsAsync(status);

            DateTime lastOccurrence = default(DateTime);
            TimeSpan pastDueAmount = TimeSpan.FromMinutes(3);
            _mockScheduleMonitor.Setup(p => p.CheckPastDueAsync(_testTimerName, It.IsAny<DateTime>(), It.IsAny<TimerSchedule>(), status))
                .Callback<string, DateTime, TimerSchedule, ScheduleStatus>((mockTimerName, mockNow, mockNext, mockStatus) =>
                    {
                        lastOccurrence = mockNow;
                    })
                .ReturnsAsync(pastDueAmount);

            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName, It.IsAny<ScheduleStatus>()))
                .Callback<string, ScheduleStatus>((mockTimerName, mockStatus) =>
                    {
                        Assert.Equal(lastOccurrence, mockStatus.Last);
                        DateTime expectedNextOccurrence = _schedule.GetNextOccurrence(lastOccurrence);
                        Assert.Equal(expectedNextOccurrence, mockStatus.Next);
                    })
                .Returns(Task.FromResult(true));

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            TimerInfo timerInfo = (TimerInfo)_triggeredFunctionData.TriggerValue;
            Assert.Same(status, timerInfo.ScheduleStatus);
            Assert.True(timerInfo.IsPastDue);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Once());

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_ScheduleNotPastDue_DoesNotInvokeJobFunctionImmediately()
        {
            var now = DateTime.Now;
            ScheduleStatus status = new ScheduleStatus
            {
                Last = now.AddHours(-1),
                Next = now.AddHours(1)
            };
            _mockScheduleMonitor.Setup(p => p.GetStatusAsync(_testTimerName)).ReturnsAsync(status);

            TimeSpan pastDueAmount = TimeSpan.Zero;
            _mockScheduleMonitor.Setup(p => p.CheckPastDueAsync(_testTimerName, It.IsAny<DateTime>(), It.IsAny<TimerSchedule>(), status))
                .ReturnsAsync(pastDueAmount);

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Never());

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_RunOnStartup_InvokesJobFunctionImmediately()
        {
            _listener.ScheduleMonitor = null;
            _attribute.RunOnStartup = true;

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Once());

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_UseMonitorFalse_DoesNotCheckForPastDueSchedule()
        {
            _listener.ScheduleMonitor = null;

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
            // invoking the job function. It's possible this is slightly lower than the exact timestamp,
            // so allow for a slight time difference.
            await _listener.HandleTimerEvent();
            double fourDays = TimeSpan.FromDays(4).TotalMilliseconds;
            Assert.InRange(_listener.Timer.Interval, fourDays - 10, fourDays);

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

        [Fact]
        public async Task Timer_CannotHaveNegativeInterval()
        {
            CreateTestListener("* * * * * *", useMonitor: true);

            ScheduleStatus status = new ScheduleStatus();
            _mockScheduleMonitor.Setup(p => p.GetStatusAsync(_testTimerName)).ReturnsAsync(status);

            // Make sure we invoke b/c we're past due.
            _mockScheduleMonitor.Setup(p => p.CheckPastDueAsync(_testTimerName, It.IsAny<DateTime>(), It.IsAny<TimerSchedule>(), status))
                .ReturnsAsync(TimeSpan.FromMilliseconds(1));

            // Use the monitor to sleep for a second. This ensures that we recalculate the Next value before
            // starting the timer. Otherwise, you can end up with a negative interval.
            bool updateCalled = false;
            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName, It.IsAny<ScheduleStatus>()))
                .Callback(() =>
                {
                    // only sleep for the first call
                    if (!updateCalled)
                    {
                        Thread.Sleep(1000);
                    }
                    updateCalled = true;
                })
                .Returns(Task.FromResult(true));

            await _listener.StartAsync(CancellationToken.None);

            Assert.True(updateCalled);
        }

        [Fact]
        public async Task StoppedListener_DoesNotContinueRunning()
        {
            // There was a bug where we would re-create a disposed _timer after a call to StopAsync(). This only
            // happened if there was a function running when StopAsync() was called.
            int count = 0;
            CreateTestListener("* * * * * *", useMonitor: false, functionAction: () =>
            {
                count++;
                _listener.StopAsync(CancellationToken.None).Wait();
            });
            await _listener.StartAsync(CancellationToken.None);
            await Task.Delay(3000);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Listener_LogsSchedule_ByDefault()
        {
            CreateTestListener("* * * * * *", useMonitor: false);

            await _listener.StartAsync(CancellationToken.None);
            await _listener.StopAsync(CancellationToken.None);

            Assert.True(_traceWriter.Events.Single(m => m.Level == TraceLevel.Info).Message.StartsWith("The next 5 occurrences of the schedule will be:"));
        }

        [Fact]
        public async Task Listener_LogsInitialStatus_WhenUsingMonitor()
        {
            var status = new ScheduleStatus
            {
                Last = new DateTime(2016, 3, 4),
                Next = new DateTime(2016, 3, 4, 0, 0, 1),
                LastUpdated = new DateTime(2016, 3, 3, 23, 59, 59)
            };

            var expected = $"Function 'Program.TestTimerJob' initial status: Last='{status.Last.ToString("o")}', Next='{status.Next.ToString("o")}', LastUpdated='{status.LastUpdated.ToString("o")}'";
            await RunInitialStatusTestAsync(status, expected);
        }

        [Fact]
        public async Task Listener_LogsInitialNullStatus_WhenUsingMonitor()
        {
            await RunInitialStatusTestAsync(null, "Function 'Program.TestTimerJob' initial status: Last='', Next='', LastUpdated=''");
        }

        public static IEnumerable<object[]> TimerSchedulesAfterDST => new object[][]
        {
            new object[] { new CronSchedule(CrontabSchedule.Parse("0 0 18 * * 5", new CrontabSchedule.ParseOptions() { IncludingSeconds = true })), TimeSpan.FromHours(167) },
            new object[] { new ConstantSchedule(TimeSpan.FromDays(7)), TimeSpan.FromDays(7) },
        };

        public static IEnumerable<object[]> TimerSchedulesWithinDST => new object[][]
        {
            new object[] { new CronSchedule(CrontabSchedule.Parse("0 59 * * * *", new CrontabSchedule.ParseOptions() { IncludingSeconds = true })), TimeSpan.FromHours(1) },
            new object[] { new ConstantSchedule(TimeSpan.FromMinutes(5)), TimeSpan.FromMinutes(5) },
        };

        /// <summary>
        /// Situation where the DST transition happens in the middle of the schedule, with the
        /// next occurrence AFTER the DST transition.
        /// </summary>
        [Theory]
        [MemberData(nameof(TimerSchedulesAfterDST))]
        public void GetNextInterval_NextAfterDST_ReturnsExpectedValue(TimerSchedule schedule, TimeSpan expectedInterval)
        {
            // This only works with a DST-supported time zone, so throw a nice exception
            if (!TimeZoneInfo.Local.SupportsDaylightSavingTime)
            {
                throw new InvalidOperationException("This test will only pass if the time zone supports DST.");
            }

            // Running on the Friday before the DST switch at 2 AM on 3/11 (Pacific Standard Time)
            // Note: this test uses Local time, so if you're running in a timezone where
            // DST doesn't transition the test might not be valid.
            // The input schedules will run after DST changes. For some (Cron), they will subtract
            // an hour to account for the shift. For others (Constant), they will not.
            var now = new DateTime(2018, 3, 9, 18, 0, 0, DateTimeKind.Local);

            var next = schedule.GetNextOccurrence(now);
            var interval = TimerListener.GetNextTimerInterval(next, now, schedule.AdjustForDST);

            // One week is normally 168 hours, but it's 167 hours across DST
            Assert.Equal(interval, expectedInterval);
        }

        /// <summary>
        /// Situation where the next occurrence falls within the hour that will be skipped
        /// as part of the DST transition (i.e. an invalid time).
        /// </summary>
        [Theory]
        [MemberData(nameof(TimerSchedulesWithinDST))]
        public void GetNextInterval_NextWithinDST_ReturnsExpectedValue(TimerSchedule schedule, TimeSpan expectedInterval)
        {
            // This only works with a DST-supported time zone, so throw a nice exception
            if (!TimeZoneInfo.Local.SupportsDaylightSavingTime)
            {
                throw new InvalidOperationException("This test will only pass if the time zone supports DST.");
            }

            // Running at 1:59 AM, i.e. one minute before the DST switch at 2 AM on 3/11 (Pacific Standard Time)
            // Note: this test uses Local time, so if you're running in a timezone where
            // DST doesn't transition the test might not be valid.
            var now = new DateTime(2018, 3, 11, 1, 59, 0, DateTimeKind.Local);

            var next = schedule.GetNextOccurrence(now);

            var interval = TimerListener.GetNextTimerInterval(next, now, schedule.AdjustForDST);
            Assert.Equal(expectedInterval, interval);
        }

        [Fact]
        public void GetNextInterval_NegativeInterval_ReturnsOneTick()
        {
            var now = DateTime.Now;
            var next = now.Subtract(TimeSpan.FromSeconds(1));

            var interval = TimerListener.GetNextTimerInterval(next, now, true);
            Assert.Equal(1, interval.Ticks);
        }

        public async Task RunInitialStatusTestAsync(ScheduleStatus initialStatus, string expected)
        {
            _mockScheduleMonitor
                .Setup(m => m.GetStatusAsync(_testTimerName))
                .ReturnsAsync(initialStatus);
            _mockScheduleMonitor
                .Setup(m => m.CheckPastDueAsync(_testTimerName, It.IsAny<DateTime>(), _schedule, It.IsAny<ScheduleStatus>()))
                .ReturnsAsync(TimeSpan.Zero);

            await _listener.StartAsync(CancellationToken.None);
            await _listener.StopAsync(CancellationToken.None);
            _listener.Dispose();

            Assert.Equal(expected, _traceWriter.Events.Single(m => m.Level == TraceLevel.Verbose).Message);
        }

        private void CreateTestListener(string expression, bool useMonitor = true, Action functionAction = null)
        {
            _attribute = new TimerTriggerAttribute(expression);
            _schedule = TimerSchedule.Create(_attribute, new TestNameResolver());
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
                    functionAction?.Invoke();
                })
                .Returns(Task.FromResult(result));
            JobHostConfiguration hostConfig = new JobHostConfiguration();
            hostConfig.HostId = "testhostid";
            _traceWriter = new TestTraceWriter();
            _listener = new TimerListener(_attribute, _schedule, _testTimerName, _config, _mockTriggerExecutor.Object, _traceWriter);
        }
    }
}
