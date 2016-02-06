// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Listeners
{
    [Singleton(Mode = SingletonMode.Listener)]
    internal sealed class TimerListener : IListener
    {
        private readonly TimerTriggerAttribute _attribute;
        private readonly TimersConfiguration _config;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly TraceWriter _trace;
        private readonly CancellationTokenSource _cancellationTokenSource;

        // Since Timer uses an integer internally for it's interval,
        // it has a maximum interval of 24.8 days.
        private static TimeSpan _maxTimerInterval = TimeSpan.FromDays(24);

        private System.Timers.Timer _timer;
        private TimerSchedule _schedule;
        private string _timerName;
        private bool _disposed;
        private TimeSpan _remainingInterval;
        private ScheduleStatus _scheduleStatus;

        public TimerListener(TimerTriggerAttribute attribute, string timerName, TimersConfiguration config, ITriggeredFunctionExecutor executor, TraceWriter trace)
        {
            _attribute = attribute;
            _timerName = timerName;
            _config = config;
            _executor = executor;
            _trace = trace;
            _cancellationTokenSource = new CancellationTokenSource();
            _schedule = _attribute.Schedule;
            ScheduleMonitor = _attribute.UseMonitor ? _config.ScheduleMonitor : null;
        }

        internal static TimeSpan MaxTimerInterval 
        {
            get
            {
                return _maxTimerInterval;
            }
        }

        // for testing
        internal System.Timers.Timer Timer
        {
            get
            {
                return _timer;
            }
        }

        internal ScheduleMonitor ScheduleMonitor { get; set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_timer != null && _timer.Enabled)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            // if schedule monitoring is enabled, record (or initialize)
            // the current schedule status
            bool isPastDue = false;
            DateTime now = DateTime.Now;
            if (ScheduleMonitor != null)
            {
                // check to see if we've missed an occurrence since we last started.
                // If we have, invoke it immediately.
                _scheduleStatus = await ScheduleMonitor.GetStatusAsync(_timerName);
                TimeSpan pastDueDuration = await ScheduleMonitor.CheckPastDueAsync(_timerName, now, _schedule, _scheduleStatus);
                isPastDue = pastDueDuration != TimeSpan.Zero;

                if (_scheduleStatus == null)
                {
                    // no schedule status has been stored yet, so initialize
                    _scheduleStatus = new ScheduleStatus
                    {
                        Last = default(DateTime),
                        Next = _schedule.GetNextOccurrence(now)
                    };
                }
            }

            if (isPastDue)
            {
                _trace.Verbose(string.Format("Function '{0}' is past due on startup. Executing now.", _timerName));
                await InvokeJobFunction(now, true);
            }
            else if (_attribute.RunOnStartup)
            {
                // The job is configured to run immediately on startup
                _trace.Verbose(string.Format("Function '{0}' is configured to run on startup. Executing now.", _timerName));
                await InvokeJobFunction(now);
            }

            // log the next several occurrences to console for visibility
            string nextOccurrences = TimerInfo.FormatNextOccurrences(_schedule, 5);
            _trace.Verbose(nextOccurrences);

            // start the timer
            now = DateTime.Now;
            DateTime nextOccurrence = _schedule.GetNextOccurrence(now);
            TimeSpan nextInterval = nextOccurrence - now;
            StartTimer(nextInterval);
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

        internal async Task HandleTimerEvent()
        {
            if (_remainingInterval != TimeSpan.Zero)
            {
                // if we're in the middle of a long interval that exceeds
                // Timer's max interval, continue the remaining interval w/o
                // invoking the function
                StartTimer(_remainingInterval);
                return;
            }

            DateTime now = DateTime.Now;
            await InvokeJobFunction(now, false);

            // restart the timer with the next schedule occurrence
            now = DateTime.Now;
            DateTime nextOccurrence = _schedule.GetNextOccurrence(now);
            TimeSpan nextInterval = nextOccurrence - now;
            StartTimer(nextInterval);
        }

        internal async Task InvokeJobFunction(DateTime lastOccurrence, bool isPastDue = false)
        {
            CancellationToken token = _cancellationTokenSource.Token;
            TimerInfo timerInfo = new TimerInfo(_attribute.Schedule, _scheduleStatus, isPastDue);
            TriggeredFunctionData input = new TriggeredFunctionData
            {
                TriggerValue = timerInfo
            };

            try
            {
                FunctionResult result = await _executor.TryExecuteAsync(input, token);
                if (!result.Succeeded)
                {
                    token.ThrowIfCancellationRequested();
                }
            }
            catch
            {
                // We don't want any function errors to stop the execution
                // schedule. Errors will be logged to Dashboard already.
            }

            if (ScheduleMonitor != null)
            {
                _scheduleStatus = new ScheduleStatus
                {
                    Last = lastOccurrence,
                    Next = _schedule.GetNextOccurrence(lastOccurrence)
                };
                await ScheduleMonitor.UpdateStatusAsync(_timerName, _scheduleStatus);
            }
        }

        private void StartTimer(TimeSpan interval)
        {
            _timer = new System.Timers.Timer
            {
                AutoReset = false
            };
            _timer.Elapsed += OnTimer;

            if (interval > MaxTimerInterval)
            {
                // if the interval exceeds the maximum interval supported by Timer,
                // store the remainder and use the max
                _remainingInterval = interval - MaxTimerInterval;
                interval = MaxTimerInterval;
            }
            else
            {
                // clear out any remaining interval
                _remainingInterval = TimeSpan.Zero;
            }

            _timer.Interval = interval.TotalMilliseconds;
            _timer.Start();
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
