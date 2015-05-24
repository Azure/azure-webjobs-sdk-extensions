using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace WebJobs.Extensions.Timers.Listeners
{
    internal sealed class TimerListener : IListener
    {
        private readonly TimerTriggerAttribute _attribute;
        private readonly TimerTriggerExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private TimerInfo _timerInfo;
        private System.Timers.Timer _timer;
        private bool _disposed;

        public TimerListener(TimerTriggerAttribute attribute, TimerTriggerExecutor triggerExecutor)
        {
            _attribute = attribute;
            _triggerExecutor = triggerExecutor;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_timer != null && _timer.Enabled)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            _timerInfo = new TimerInfo
            {
                Schedule = _attribute.Schedule
            };

            _timer = new System.Timers.Timer
            {
                AutoReset = false,
                Interval = GetNextInterval(DateTime.Now)
            };
            _timer.Elapsed += OnTimer;
            _timer.Start();

            return Task.FromResult<bool>(true);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_timer == null)
            {
                throw new InvalidOperationException("The listener has not yet been started or has already been stopped.");
            }

            // Signal ProcessMessage to shut down gracefully
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

        void OnTimer(object sender, ElapsedEventArgs e)
        {
            HandleTimerEvent().Wait();
        }

        private async Task HandleTimerEvent()
        {
            CancellationToken token = _cancellationTokenSource.Token;

            // TODO: need to construct new instances?
            DateTime lastOccurrence = DateTime.Now;
            TimerInfo timerInfo = new TimerInfo
            {
                Schedule = _attribute.Schedule
            };

            if (!await _triggerExecutor.ExecuteAsync(timerInfo, token))
            {
                token.ThrowIfCancellationRequested();
            }

            _timer.Interval = GetNextInterval(lastOccurrence);
            _timer.Start();
        }

        private double GetNextInterval(DateTime now)
        {
            DateTime nextOccurrence = _timerInfo.Schedule.GetNextOccurrence(now);
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
