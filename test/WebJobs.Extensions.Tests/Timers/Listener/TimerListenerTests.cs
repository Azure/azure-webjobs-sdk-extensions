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
        private Mock<ITriggeredFunctionExecutor<TimerInfo>> _mockTriggerExecutor;
        private TriggeredFunctionData<TimerInfo> _triggeredFunctionData;

        public TimerListenerTests()
        {
            _attribute = new TimerTriggerAttribute("0 */1 * * * *");
            _config = new TimersConfiguration();
            _mockScheduleMonitor = new Mock<ScheduleMonitor>(MockBehavior.Strict);
            _config.ScheduleMonitor = _mockScheduleMonitor.Object;
            _mockTriggerExecutor = new Mock<ITriggeredFunctionExecutor<TimerInfo>>(MockBehavior.Strict);
            TimerTriggerExecutor executor = new TimerTriggerExecutor(_mockTriggerExecutor.Object);
            FunctionResult result = new FunctionResult(true);
            _mockTriggerExecutor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<TimerInfo>>(), It.IsAny<CancellationToken>()))
                .Callback<TriggeredFunctionData<TimerInfo>, CancellationToken>((mockFunctionData, mockToken) =>
                    {
                        _triggeredFunctionData = mockFunctionData;
                    })
                .Returns(Task.FromResult(result));
            _listener = new TimerListener(_attribute, _testTimerName, _config, executor);
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

            _mockScheduleMonitor.Setup(p => p.IsPastDueAsync(_testTimerName, It.IsAny<DateTime>()))
                .Callback<string, DateTime>((mockTimerName, mockNow) =>
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

            Assert.True(_triggeredFunctionData.TriggerValue.IsPastDue);

            _listener.Dispose();
        }

        [Fact]
        public async Task StartAsync_ScheduleNotPastDue_DoesNotInvokeJobFunctionImmediately()
        {
            _mockScheduleMonitor.Setup(p => p.IsPastDueAsync(_testTimerName, It.IsAny<DateTime>()))
                .Returns(Task.FromResult(false));

            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StartAsync(cancellationToken);

            _mockTriggerExecutor.Verify(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData<TimerInfo>>(), It.IsAny<CancellationToken>()), Times.Never());

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
    }
}
