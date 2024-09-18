// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;
using Moq;
using NCrontab;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers
{
    public class TimerListenerTests : IDisposable
    {
        private readonly string _testTimerName = "Program.TestTimerJob";
        private readonly string _functionShortName = "TimerFunctionShortName";
        private TimerListener _listener;
        private Mock<ScheduleMonitor> _mockScheduleMonitor;
        private TimersOptions _options;
        private TimerTriggerAttribute _attribute;
        private TimerSchedule _schedule;
        private Mock<ITriggeredFunctionExecutor> _mockTriggerExecutor;
        private TriggeredFunctionData _triggeredFunctionData;
        private TestLogger _logger;

        public TimerListenerTests()
        {
            CreateTestListener("0 */1 * * * *");
        }

        [Fact]
        public async Task InvokeJobFunction_UpdatesScheduleMonitor()
        {
            DateTimeOffset lastOccurrence = DateTimeOffset.Now;
            DateTimeOffset nextOccurrence = _schedule.GetNextOccurrence(lastOccurrence.LocalDateTime);

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
            DateTimeOffset invocationTime = status.Next.AddMilliseconds(-1);

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
        public async Task HandleTimerEvent_HandlesExceptions()
        {
            // force an exception to occur outside of the function invocation path
            var ex = new Exception("Kaboom!");
            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName, It.IsAny<ScheduleStatus>())).ThrowsAsync(ex);

            var listener = new TimerListener(_attribute, _schedule, _testTimerName, _options, _mockTriggerExecutor.Object, _logger, _mockScheduleMonitor.Object, _functionShortName);

            Assert.Null(listener.Timer);

            await listener.HandleTimerEvent();

            // verify the timer was started
            Assert.NotNull(listener.Timer);
            Assert.True(listener.Timer.Enabled);

            var logs = _logger.GetLogMessages();
            var log = logs[2];
            Assert.Equal(LogLevel.Error, log.Level);
            Assert.Equal("Error occurred during scheduled invocation for 'TimerFunctionShortName'.", log.FormattedMessage);
            Assert.Same(ex, log.Exception);
            log = logs[3];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.True(log.FormattedMessage.StartsWith("Timer for 'TimerFunctionShortName' started with interval"));

            listener.Dispose();
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
            DateTimeOffset invocationTime = status.Next.AddMilliseconds(-1);
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
        public async Task StartAsync_SchedulePastDue_SchedulesImmediateInvocation()
        {
            // Set this to true to ensure that the function is only executed once
            // In this case, because it is run on startup due to being behind schedule,
            // it shouldn't be run twice.
            _attribute.RunOnStartup = true;

            ScheduleStatus status = new ScheduleStatus();
            _mockScheduleMonitor.Setup(p => p.GetStatusAsync(_testTimerName)).ReturnsAsync(status);

            DateTimeOffset lastOccurrence = default(DateTimeOffset);
            TimeSpan pastDueAmount = TimeSpan.FromMinutes(3);
            _mockScheduleMonitor.Setup(p => p.CheckPastDueAsync(_testTimerName, It.IsAny<DateTimeOffset>(), It.IsAny<TimerSchedule>(), status))
                .Callback<string, DateTimeOffset, TimerSchedule, ScheduleStatus>((mockTimerName, mockNow, mockNext, mockStatus) =>
                    {
                        lastOccurrence = mockNow;
                    })
                .ReturnsAsync(pastDueAmount);

            _mockScheduleMonitor.Setup(p => p.UpdateStatusAsync(_testTimerName, It.IsAny<ScheduleStatus>()))
                .Callback<string, ScheduleStatus>((mockTimerName, mockStatus) =>
                    {
                        Assert.Equal(lastOccurrence, mockStatus.Last);
                        DateTimeOffset expectedNextOccurrence = _schedule.GetNextOccurrence(lastOccurrence.LocalDateTime);
                        Assert.Equal(expectedNextOccurrence, mockStatus.Next);
                    })
                .Returns(Task.FromResult(true));

            CancellationToken cancellationToken = CancellationToken.None;
            await _listener.StartAsync(cancellationToken);

            var startupInvocation = _listener.StartupInvocation;
            Assert.NotNull(startupInvocation);
            Assert.False(startupInvocation.RunOnStartup);
            Assert.Equal(TimerListener.StartupInvocationContext.IntervalMS, _listener.Timer.Interval);
            Assert.True(startupInvocation.IsPastDue);
            Assert.Equal(default(DateTimeOffset).ToLocalTime(), startupInvocation.OriginalSchedule);

            await Task.Delay(100);

            TimerInfo timerInfo = (TimerInfo)_triggeredFunctionData.TriggerValue;
            Assert.Same(status, timerInfo.ScheduleStatus);
            Assert.True(timerInfo.IsPastDue);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Once());

            // Make sure we've added the reason for the invocation into the Details
            Assert.Equal(default(DateTimeOffset).ToLocalTime().ToString("o"), _triggeredFunctionData.TriggerDetails[TimerListener.OriginalScheduleKey]);
            Assert.Equal("IsPastDue", _triggeredFunctionData.TriggerDetails[TimerListener.UnscheduledInvocationReasonKey]);

            Assert.Null(_listener.StartupInvocation);

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_ScheduleNotPastDue_DoesNotScheduleImmediateInvocation()
        {
            var now = DateTime.Now;
            ScheduleStatus status = new ScheduleStatus
            {
                Last = now.AddHours(-1),
                Next = now.AddHours(1)
            };
            _mockScheduleMonitor.Setup(p => p.GetStatusAsync(_testTimerName)).ReturnsAsync(status);

            TimeSpan pastDueAmount = TimeSpan.Zero;
            _mockScheduleMonitor.Setup(p => p.CheckPastDueAsync(_testTimerName, It.IsAny<DateTimeOffset>(), It.IsAny<TimerSchedule>(), status))
                .ReturnsAsync(pastDueAmount);

            CancellationToken cancellationToken = CancellationToken.None;
            await _listener.StartAsync(cancellationToken);

            Assert.Null(_listener.StartupInvocation);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Never());

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_RunOnStartup_SchedulesImmediateInvocation()
        {
            _listener.ScheduleMonitor = null;
            _attribute.RunOnStartup = true;

            CancellationToken cancellationToken = CancellationToken.None;
            await _listener.StartAsync(cancellationToken);

            var startupInvocation = _listener.StartupInvocation;
            Assert.NotNull(startupInvocation);
            Assert.True(startupInvocation.RunOnStartup);
            Assert.Equal(TimerListener.StartupInvocationContext.IntervalMS, _listener.Timer.Interval);
            Assert.False(startupInvocation.IsPastDue);
            Assert.Equal(default(DateTimeOffset), startupInvocation.OriginalSchedule);

            await Task.Delay(100);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()), Times.Once());

            // Make sure we've added the reason for the invocation into the Details
            Assert.False(_triggeredFunctionData.TriggerDetails.ContainsKey(TimerListener.OriginalScheduleKey));
            Assert.Equal("RunOnStartup", _triggeredFunctionData.TriggerDetails[TimerListener.UnscheduledInvocationReasonKey]);

            Assert.Null(_listener.StartupInvocation);

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_UseMonitorFalse_DoesNotCheckForPastDueSchedule()
        {
            _listener.ScheduleMonitor = null;

            CancellationToken cancellationToken = CancellationToken.None;
            await _listener.StartAsync(cancellationToken);

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_ScheduleStatus_DateKindIsLocal()
        {
            _listener.ScheduleMonitor = null;

            CancellationToken cancellationToken = CancellationToken.None;
            await _listener.StartAsync(cancellationToken);

            //Assert.Equal(DateTimeKind.Local, _listener.ScheduleStatus.Last.Kind);
            //Assert.Equal(DateTimeKind.Local, _listener.ScheduleStatus.Next.Kind);
            //Assert.Equal(DateTimeKind.Local, _listener.ScheduleStatus.LastUpdated.Kind);

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_ExtendedScheduleInterval_TimerContinuesUntilTotalIntervalComplete()
        {
            // create a timer with an extended interval that exceeds the max
            TimeSpan interval = TimerListener.MaxTimerInterval + TimerListener.MaxTimerInterval + TimeSpan.FromDays(4);
            CreateTestListener(interval.ToString(), useMonitor: false);

            CancellationToken cancellationToken = CancellationToken.None;
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
            _mockScheduleMonitor.Setup(p => p.CheckPastDueAsync(_testTimerName, It.IsAny<DateTimeOffset>(), It.IsAny<TimerSchedule>(), status))
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

            await TestHelpers.Await(() =>
            {
                return updateCalled;
            });
        }

        [Fact]
        public async Task StopAsync_AllowsOutstandingInvocationToComplete()
        {
            bool invocationStarted = false;
            bool invocationCompleted = false;
            CreateTestListener("* * * * * *", useMonitor: false, functionAction: () =>
            {
                invocationStarted = true;
                Task.Delay(3000).Wait();
                invocationCompleted = true;
            });

            await _listener.StartAsync(CancellationToken.None);

            await TestHelpers.Await(() => invocationStarted, pollingInterval: 500);

            // after the function has started running, stop the listener
            await _listener.StopAsync(CancellationToken.None);

            // ensure the invocation was allowed to complete
            Assert.True(invocationCompleted, "Outstanding invocation wasn't allowed to complete");

            var logMessages = _logger.GetLogMessages().Select(p => p.FormattedMessage).ToArray();
            Assert.StartsWith("The 'TimerFunctionShortName' timer is using the schedule 'Cron: '* * * * * *''", logMessages[0]);
            Assert.StartsWith("The next 5 occurrences of the 'TimerFunctionShortName' schedule (Cron: '* * * * * *') will be", logMessages[1]);
            Assert.StartsWith("Timer for 'TimerFunctionShortName' started", logMessages[2]);
            Assert.StartsWith("Timer listener started (TimerFunctionShortName)", logMessages[3]);
            Assert.StartsWith("Function invocation starting", logMessages[4]);
            Assert.StartsWith("Function invocation complete", logMessages[5]);
            Assert.StartsWith("Timer listener stopped (TimerFunctionShortName)", logMessages[6]);
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

            LogMessage actualMessage = _logger.GetLogMessages().Single(m => m.Level == LogLevel.Information);
            Assert.StartsWith($"The next 5 occurrences of the '{_functionShortName}' schedule ({_schedule}) will be:", actualMessage.FormattedMessage);

            // make sure we're logging function name.
            Assert.Equal(_functionShortName, actualMessage.State.Single(p => p.Key == "functionName").Value);
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

            var expected = $"Function '{_functionShortName}' initial status: Last='{status.Last.ToString("o")}', Next='{status.Next.ToString("o")}', LastUpdated='{status.LastUpdated.ToString("o")}'";
            await RunInitialStatusTestAsync(status, expected);
        }

        [Fact]
        public async Task Listener_LogsInitialNullStatus_WhenUsingMonitor()
        {
            await RunInitialStatusTestAsync(null, $"Function '{_functionShortName}' initial status: Last='(null)', Next='(null)', LastUpdated='(null)'");
        }

        public static IEnumerable<object[]> TimerSchedulesEnteringDST =>
        [
            [new CronSchedule(CrontabSchedule.Parse("0 0 18 * * 5", new CrontabSchedule.ParseOptions() { IncludingSeconds = true })), TimeSpan.FromHours(167)],
            [new ConstantSchedule(TimeSpan.FromDays(7)), TimeSpan.FromDays(7)],
        ];

        public static IEnumerable<object[]> TimerSchedulesWithinDST =>
        [
            [new CronSchedule(CrontabSchedule.Parse("0 59 * * * *", new CrontabSchedule.ParseOptions() { IncludingSeconds = true })), TimeSpan.FromHours(1)],
            [new ConstantSchedule(TimeSpan.FromMinutes(5)), TimeSpan.FromMinutes(5)],
        ];

        // Note: In Pacific time, DST (UTC -7) ends at 2 AM on 11/4/2018. The clocks go back to 1 AM (with UTC -8).
        public static IEnumerable<object[]> CronTimerSchedulesExitingDST =>
        [
            // Starts in ambiguous PDT, ends in PST. Run every hour at the 30 minute mark.
            [new DateTimeOffset(2018, 11, 4, 1, 31, 0, TimeSpan.FromHours(-7)), "0 30 * * * *", TimeSpan.FromMinutes(59)],

            // Starts in ambiguous PDT, ends in PST. Run every minute.
            [new DateTimeOffset(2018, 11, 4, 1, 59, 0, TimeSpan.FromHours(-7)), "0 * * * * *", TimeSpan.FromMinutes(1)],

            // Starts in ambiguous PDT, ends in ambiguous PDT. Run every minute.
            [new DateTimeOffset(2018, 11, 4, 1, 30, 0, TimeSpan.FromHours(-7)), "0 * * * * *", TimeSpan.FromMinutes(1)],

            // Starts in ambiguous PDT, ends in ambiguous PST. Run at 1:00/1:30/2:00/2:30. This is considered an interval and should
            // should run 6 times during the PDT -> PST transition.
            [new DateTimeOffset(2018, 11, 4, 1, 45, 0, TimeSpan.FromHours(-7)), "0 0,30 1-2 * * *", TimeSpan.FromMinutes(15)],

            // Starts in ambiguous PST, ends in ambiguous PST. Run at 1:30/2:30. This is considered an interval and should
            // should run 6 times during the PDT -> PST transition. No log is expected because no adjustment is performed as the
            // offsets match in this case.
            [new DateTimeOffset(2018, 11, 4, 1, 45, 0, TimeSpan.FromHours(-8)), "0 0,30 1-3 * * *", TimeSpan.FromMinutes(15)],

            // Starts in ambiguous PST, ends in ambiguous PDT. Run every minute for a range.
            [new DateTimeOffset(2018, 11, 4, 1, 30, 0, TimeSpan.FromHours(-7)), "0 * 1-3 * * *", TimeSpan.FromMinutes(1)],

            // Starts in PDT, ends in ambiguous PDT. Run every minute.
            [new DateTimeOffset(2018, 11, 4, 0, 59, 0, TimeSpan.FromHours(-7)), "0 * * * * *", TimeSpan.FromMinutes(1)],

            // Starts in ambiguous PST, ends in PST. Run every hour at the 30 minute mark.
            [new DateTimeOffset(2018, 11, 4, 1, 31, 0, TimeSpan.FromHours(-8)), "0 30 * * * *", TimeSpan.FromMinutes(59)],

            // Starts in PDT, ends in PST. Run every Friday at 6 PM.
            [new DateTimeOffset(2018, 11, 2, 18, 0, 0, TimeSpan.FromHours(-7)), "0 0 18 * * 5", TimeSpan.FromHours(169)],

            // Starts in ambiguous PDT, ends in PST. Run every day at 1:30 (only expect one invocation during ambiguous times).
            [new DateTimeOffset(2018, 11, 4, 1, 30, 0, TimeSpan.FromHours(-7)), "0 30 1 * * *", TimeSpan.FromHours(25)],

            // Starts in PDT, ends in ambgiguous PDT. Run every day at 1:30 (only expect one invocation during ambiguous times).
            [new DateTimeOffset(2018, 11, 4, 0, 30, 0, TimeSpan.FromHours(-7)), "0 30 1 * * *", TimeSpan.FromHours(1)],

            // Starts in ambiguous PDT, ends in ambgiguous PDT. Run every day at 1:30 (only expect one invocation during ambiguous times).
            [new DateTimeOffset(2018, 11, 4, 1, 29, 0, TimeSpan.FromHours(-7)), "0 30 1 * * *", TimeSpan.FromMinutes(1)],

            // Starts in ambiguous PST, ends in PST. Run every day at 1:30 (only expect one invocation during ambiguous times).
            [new DateTimeOffset(2018, 11, 4, 1, 29, 0, TimeSpan.FromHours(-8)), "0 30 1 * * *", TimeSpan.Parse("1.00:01")],

            // Starts in ambiguous PDT, ends in PST. Run every day at 1:30 (only expect one invocation during ambiguous times).
            [new DateTimeOffset(2018, 11, 4, 1, 31, 0, TimeSpan.FromHours(-7)), "0 30 1 * * *", TimeSpan.Parse("1.00:59")],
        ];

        /// <summary>
        /// Situation where the DST transition happens in the middle of the schedule, with the
        /// next occurrence AFTER the DST transition.
        /// </summary>
        [Theory]
        [MemberData(nameof(TimerSchedulesEnteringDST))]
        public void GetNextInterval_NextAfterDSTBegins_ReturnsExpectedValue(TimerSchedule schedule, TimeSpan expectedInterval)
        {
            SetLocalTimeZoneToPacific();

            // Running on the Friday before DST *begins* at 2 AM on 3/11 (Pacific Standard Time)
            // Note: this test uses Local time, so if you're running in a timezone where
            // DST doesn't transition the test might not be valid.
            // The input schedules will run after DST changes. For some (Cron), they will subtract
            // an hour to account for the shift. For others (Constant), they will not.
            var start = new DateTime(2018, 3, 9, 18, 0, 0, DateTimeKind.Local);
            var now = new DateTimeOffset(start);

            var next = schedule.GetNextOccurrence(now.LocalDateTime);
            var interval = TimerListener.GetNextTimerInterval(next, now);

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
            SetLocalTimeZoneToPacific();

            // Running at 1:59 AM, i.e. one minute before the DST switch at 2 AM on 3/11 (Pacific Standard Time)
            // Note: this test uses Local time, so if you're running in a timezone where
            // DST doesn't transition the test might not be valid.
            var start = new DateTime(2018, 3, 11, 1, 59, 0, DateTimeKind.Local);
            var now = new DateTimeOffset(start);

            var next = schedule.GetNextOccurrence(now.DateTime);

            TestLogger logger = new(null);
            var interval = TimerListener.GetNextTimerInterval(next, now);
            Assert.Equal(expectedInterval, interval);
        }

        /// <summary>
        /// Situation where we exit DST and the next occurrence is in the hour that is repeated.
        /// </summary>
        [Theory]
        [MemberData(nameof(CronTimerSchedulesExitingDST))]
        public void GetNextInterval_NextAfterDSTEnds_ReturnsExpectedValue(DateTimeOffset now, string cronSchedule, TimeSpan expectedInterval)
        {
            SetLocalTimeZoneToPacific();

            var schedule = new CronSchedule(CrontabSchedule.Parse(cronSchedule, new CrontabSchedule.ParseOptions() { IncludingSeconds = true }));

            var logger = new TestLogger(null);
            var next = schedule.GetNextOccurrence(now.LocalDateTime);
            var interval = TimerListener.GetNextTimerInterval(next, now);

            Assert.Equal(expectedInterval, interval);
        }

        [Fact]
        public void GetNextInterval_NegativeInterval_ReturnsOneTick()
        {
            var now = DateTimeOffset.Now;
            var next = now.Subtract(TimeSpan.FromSeconds(1)).LocalDateTime;

            var interval = TimerListener.GetNextTimerInterval(next, now);
            Assert.Equal(1, interval.Ticks);
        }

        public async Task RunInitialStatusTestAsync(ScheduleStatus initialStatus, string expected)
        {
            _mockScheduleMonitor
                .Setup(m => m.GetStatusAsync(_testTimerName))
                .ReturnsAsync(initialStatus);
            _mockScheduleMonitor
                .Setup(m => m.CheckPastDueAsync(_testTimerName, It.IsAny<DateTimeOffset>(), _schedule, It.IsAny<ScheduleStatus>()))
                .ReturnsAsync(TimeSpan.Zero);

            await _listener.StartAsync(CancellationToken.None);
            await _listener.StopAsync(CancellationToken.None);
            _listener.Dispose();

            LogMessage[] verboseTraces = _logger.GetLogMessages()
                .Where(m => m.Level == LogLevel.Debug)
                .OrderBy(t => t.Timestamp)
                .ToArray();

            Assert.Equal(5, verboseTraces.Length);
            Assert.Contains("timer is using the schedule 'Cron: '0 * * * * *'' and the local time zone:", verboseTraces[0].FormattedMessage);
            Assert.Equal(expected, verboseTraces[1].FormattedMessage);
            Assert.Contains($"Timer for '{_functionShortName}' started with interval", verboseTraces[2].FormattedMessage);
        }

        private void CreateTestListener(string expression, bool useMonitor = true, bool runOnStartup = false, Action functionAction = null)
        {
            _attribute = new TimerTriggerAttribute(expression)
            {
                RunOnStartup = runOnStartup
            };

            _schedule = TimerSchedule.Create(_attribute, new TestNameResolver(), _logger);
            _attribute.UseMonitor = useMonitor;
            _options = new TimersOptions();
            _mockScheduleMonitor = new Mock<ScheduleMonitor>(MockBehavior.Strict);
            _mockTriggerExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            FunctionResult result = new FunctionResult(true);
            _mockTriggerExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Callback<TriggeredFunctionData, CancellationToken>((mockFunctionData, mockToken) =>
                {
                    _logger.LogDebug("Function invocation starting");
                    _triggeredFunctionData = mockFunctionData;
                    functionAction?.Invoke();
                    _logger.LogDebug("Function invocation complete");
                })
                .Returns(Task.FromResult(result));
            _logger = new TestLogger(null);
            _listener = new TimerListener(_attribute, _schedule, _testTimerName, _options, _mockTriggerExecutor.Object, _logger, _mockScheduleMonitor.Object, _functionShortName);
        }

        internal static void SetLocalTimeZoneToPacific()
        {
            // There are so many internal benefits to using DateTimeKind.Local for us, that we're relying 
            // on it to provide the proper roundtripping support between DateTime and DateTimeOffset. This appears
            // to be the only way to "mock" this value as it's hard-coded inside a lot of .NET libraries when
            // calculating offsets, time zones, etc.
            var info = typeof(TimeZoneInfo).GetField("s_cachedData", BindingFlags.NonPublic | BindingFlags.Static);
            var cachedData = info.GetValue(null);
            var field = cachedData.GetType().GetField("_localTimeZone", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(cachedData, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        }

        public void Dispose() => TimeZoneInfo.ClearCachedData();
    }
}
