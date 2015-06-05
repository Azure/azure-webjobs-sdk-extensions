using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Config;
using Microsoft.Azure.WebJobs.Extensions.Timers.Scheduling;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Listeners
{
    internal sealed class TimerListener : IListener
    {
        private readonly TimerTriggerAttribute _attribute;
        private readonly TimersConfiguration _config;
        private readonly TimerTriggerExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private System.Timers.Timer _timer;
        private TimerSchedule _schedule;
        private ScheduleMonitor _scheduleMonitor;
        private string _timerName;
        private bool _disposed;

        public TimerListener(TimerTriggerAttribute attribute, string timerName, TimersConfiguration config, TimerTriggerExecutor triggerExecutor)
        {
            _attribute = attribute;
            _timerName = timerName;
            _config = config;
            _triggerExecutor = triggerExecutor;
            _cancellationTokenSource = new CancellationTokenSource();

            _schedule = _attribute.Schedule;
            _scheduleMonitor = _config.ScheduleMonitor;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_timer != null && _timer.Enabled)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            // if schedule monitoring is enabled for this timer job,
            // check to see if we've missed an occurrence since
            // we last started
            DateTime now = DateTime.UtcNow;
            if (_attribute.UseMonitor && await _scheduleMonitor.IsPastDueAsync(_timerName, now))
            {
                // we've missed an occurrence so invoke the job function immediately
                await InvokeJobFunction(now, true);
            }

            _timer = new System.Timers.Timer
            {
                AutoReset = false,
                Interval = GetNextInterval(now)
            };
            _timer.Elapsed += OnTimer;
            _timer.Start();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_timer == null)
            {
                throw new InvalidOperationException("The listener has not yet been started or has already been stopped.");
            }

            _cancellationTokenSource.Cancel();

            _timer.Dispose();
            _timer = null;

            return Task.FromResult<bool>(true);
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Running callers might still be using the cancellation token.
                // Mark it canceled but don't dispose of the source while the callers are running.
                // Otherwise, callers would receive ObjectDisposedException when calling token.Register.
                // For now, rely on finalization to clean up _cancellationTokenSource's wait handle (if allocated).
                _cancellationTokenSource.Cancel();

                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }

                _disposed = true;
            }
        }

        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            HandleTimerEvent().Wait();
        }

        private async Task HandleTimerEvent()
        {
            DateTime lastOccurrence = DateTime.Now;

            await InvokeJobFunction(lastOccurrence, false);

            _timer.Interval = GetNextInterval(lastOccurrence);
            _timer.Start();
        }

        internal async Task InvokeJobFunction(DateTime lastOccurrence, bool isPastDue)
        {
            CancellationToken token = _cancellationTokenSource.Token;

            TimerInfo timerInfo = new TimerInfo(_attribute.Schedule);
            timerInfo.IsPastDue = isPastDue;
            FunctionResult result = await _triggerExecutor.ExecuteAsync(timerInfo, token);
            if (!result.Succeeded)
            {
                token.ThrowIfCancellationRequested();
            }

            if (_attribute.UseMonitor)
            {
                DateTime nextOccurrence = _schedule.GetNextOccurrence(lastOccurrence);
                await _scheduleMonitor.UpdateAsync(_timerName, lastOccurrence, nextOccurrence);
            }
        }

        private double GetNextInterval(DateTime now)
        {
            DateTime nextOccurrence = _schedule.GetNextOccurrence(now);
            TimeSpan nextInterval = nextOccurrence - now;
            return nextInterval.TotalMilliseconds;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }
    }
}
