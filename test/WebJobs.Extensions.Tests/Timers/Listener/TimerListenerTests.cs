using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Config;
using Microsoft.Azure.WebJobs.Extensions.Timers.Listeners;
using Microsoft.Azure.WebJobs.Extensions.Timers.Scheduling;
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
            DateTime lastOccurrence = DateTime.UtcNow;
            DateTime nextOccurrence = _attribute.Schedule.GetNextOccurrence(lastOccurrence);

            _mockScheduleMonitor.Setup(p => p.UpdateAsync(_testTimerName, lastOccurrence, nextOccurrence)).Returns(Task.FromResult(true));

            await _listener.InvokeJobFunction(lastOccurrence, false);

            _listener.Dispose();
        }

        [Fact]
        public async Task InvokeJobFunction_UseMonitorFalse_DoesNotUpdateScheduleMonitor()
        {
            _attribute.UseMonitor = false;

            await _listener.InvokeJobFunction(DateTime.UtcNow, false);

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_SchedulePastDue_InvokesJobFunctionImmediately()
        {
            DateTime lastOccurrence = default(DateTime);

            _mockScheduleMonitor.Setup(p => p.IsPastDueAsync(_testTimerName, It.IsAny<DateTime>(), It.IsAny<TimerSchedule>()))
                .Callback<string, DateTime, TimerSchedule>((mockTimerName, mockNow, mockNext) =>
                    {
                        lastOccurrence = mockNow;
                    })
                .Returns(Task.FromResult(true));

            _mockScheduleMonitor.Setup(p => p.UpdateAsync(_testTimerName, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Callback<string, DateTime, DateTime>((mockTimerName, mockLastOccurrence, mockNextOccurrence) =>
                    {
                        Assert.Equal(lastOccurrence, mockLastOccurrence);
                        DateTime expectedNextOccurrence = _attribute.Schedule.GetNextOccurrence(lastOccurrence);
                        Assert.Equal(expectedNextOccurrence, mockNextOccurrence);
                    })
                .Returns(Task.FromResult(true));

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            TimerInfo timerInfo = (TimerInfo)_triggeredFunctionData.TriggerValue;
            Assert.True(timerInfo.IsPastDue);

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
            TimerTriggerExecutor executor = new TimerTriggerExecutor(_mockTriggerExecutor.Object);
            FunctionResult result = new FunctionResult(true);
            _mockTriggerExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Callback<TriggeredFunctionData, CancellationToken>((mockFunctionData, mockToken) =>
                {
                    _triggeredFunctionData = mockFunctionData;
                })
                .Returns(Task.FromResult(result));
            _listener = new TimerListener(_attribute, _testTimerName, _config, executor);
        }
    }
}
