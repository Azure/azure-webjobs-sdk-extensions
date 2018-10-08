// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Listeners
{
    [Singleton(Mode = SingletonMode.Listener)]
    internal sealed class TimerListener : IListener
    {
        private readonly TimerTriggerAttribute _attribute;
        private readonly TimersOptions _options;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _timerName;

        // Since Timer uses an integer internally for it's interval,
        // it has a maximum interval of 24.8 days.
        private static readonly TimeSpan _maxTimerInterval = TimeSpan.FromDays(24);

        private System.Timers.Timer _timer;
        private TimerSchedule _schedule;

        private bool _disposed;
        private TimeSpan _remainingInterval;

        public TimerListener(TimerTriggerAttribute attribute, TimerSchedule schedule, string timerName, TimersOptions options, ITriggeredFunctionExecutor executor, ILogger logger, ScheduleMonitor scheduleMonitor)
        {
            _attribute = attribute;
            _timerName = timerName;
            _options = options;
            _executor = executor;
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
            _schedule = schedule;
            ScheduleMonitor = _attribute.UseMonitor ? scheduleMonitor : null;
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

        internal ScheduleStatus ScheduleStatus { get; set; }

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

            // we use DateTime.Now rather than DateTime.UtcNow to allow the local machine to set the time zone. In Azure this will be
            // UTC by default, but can be configured to use any time zone if it makes scheduling easier.
            DateTime now = DateTime.Now;
            _logger.LogDebug($"The '{_timerName}' timer is using the local time zone: '{TimeZoneInfo.Local.DisplayName}'");

            if (ScheduleMonitor != null)
            {
                // check to see if we've missed an occurrence since we last started.
                // If we have, invoke it immediately.
                ScheduleStatus = await ScheduleMonitor.GetStatusAsync(_timerName);
                _logger.LogDebug($"Function '{_timerName}' initial status: Last='{ScheduleStatus?.Last.ToString("o")}', Next='{ScheduleStatus?.Next.ToString("o")}', LastUpdated='{ScheduleStatus?.LastUpdated.ToString("o")}'");
                TimeSpan pastDueDuration = await ScheduleMonitor.CheckPastDueAsync(_timerName, now, _schedule, ScheduleStatus);
                isPastDue = pastDueDuration != TimeSpan.Zero;
            }

            if (ScheduleStatus == null)
            {
                // no schedule status has been stored yet, so initialize
                ScheduleStatus = new ScheduleStatus
                {
                    Last = default(DateTime),
                    Next = _schedule.GetNextOccurrence(now)
                };
            }

            if (isPastDue)
            {
                _logger.LogDebug($"Function '{_timerName}' is past due on startup. Executing now.");
                await InvokeJobFunction(now, isPastDue: true);
            }
            else if (_attribute.RunOnStartup)
            {
                // The job is configured to run immediately on startup
                _logger.LogDebug($"Function '{_timerName}' is configured to run on startup. Executing now.");
                await InvokeJobFunction(now, runOnStartup: true);
            }

            // log the next several occurrences to console for visibility
            string nextOccurrences = TimerInfo.FormatNextOccurrences(_schedule, 5, timerName: _timerName);
            _logger.LogInformation(nextOccurrences);

            StartTimer(DateTime.Now);
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

            await InvokeJobFunction(DateTime.Now, false);

            StartTimer(DateTime.Now);
        }

        /// <summary>
        /// Invokes the job function.
        /// </summary>
        /// <param name="invocationTime">The time of the invocation, likely DateTime.Now.</param>
        /// <param name="isPastDue">True if the invocation is because the invocation is due to a past due timer.</param>
        /// <param name="runOnStartup">True if the invocation is because the timer is configured to run on startup.</param>
        internal async Task InvokeJobFunction(DateTime invocationTime, bool isPastDue = false, bool runOnStartup = false)
        {
            CancellationToken token = _cancellationTokenSource.Token;
            ScheduleStatus timerInfoStatus = null;
            if (ScheduleMonitor != null)
            {
                timerInfoStatus = ScheduleStatus;
            }
            TimerInfo timerInfo = new TimerInfo(_schedule, timerInfoStatus, isPastDue);
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

            // If the trigger fired before it was officially scheduled (likely under 1 second due to clock skew),
            // adjust the invocation time forward for the purposes of calculating the next occurrence.
            // Without this, it's possible to set the 'Next' value to the same time twice in a row, 
            // which results in duplicate triggers if the site restarts.
            DateTime adjustedInvocationTime = invocationTime;
            if (!isPastDue && !runOnStartup && ScheduleStatus?.Next > invocationTime)
            {
                adjustedInvocationTime = ScheduleStatus.Next;
            }

            // Create the Last value with the adjustedInvocationTime; otherwise, the listener will
            // consider this a schedule change when the host next starts.
            ScheduleStatus = new ScheduleStatus
            {
                Last = adjustedInvocationTime,
                Next = _schedule.GetNextOccurrence(adjustedInvocationTime),
                LastUpdated = adjustedInvocationTime
            };

            if (ScheduleMonitor != null)
            {
                await ScheduleMonitor.UpdateStatusAsync(_timerName, ScheduleStatus);
                _logger.LogDebug($"Function '{_timerName}' updated status: Last='{ScheduleStatus.Last.ToString("o")}', Next='{ScheduleStatus.Next.ToString("o")}', LastUpdated='{ScheduleStatus.LastUpdated.ToString("o")}'");
            }
        }

        private void StartTimer(DateTime now)
        {
            var nextInterval = GetNextTimerInterval(ScheduleStatus.Next, now, _schedule.AdjustForDST);
            StartTimer(nextInterval);
        }

        /// <summary>
        /// Calculate the next timer interval based on the current (Local) time.
        /// </summary>
        /// <remarks>
        /// We calculate based on the current time because we don't know how long
        /// the previous function invocation took. Example: if you have an hourly timer
        /// invoked at 12:00 and the invocation takes 1 minute, we want to calculate
        /// the interval for the next timer using 12:01 rather than at 12:00. Otherwise, 
        /// you'd start a 1-hour timer at 12:01 when we really want it to be a 59-minute timer.
        /// </remarks>
        /// <param name="next">The next schedule occurrence in Local time</param>
        /// <param name="now">The current Local time</param>
        /// <returns>The next timer interval</returns>
        internal static TimeSpan GetNextTimerInterval(DateTime next, DateTime now, bool adjustForDST)
        {
            TimeSpan nextInterval;

            if (adjustForDST)
            {
                // For calculations, we use DateTimeOffsets and TimeZoneInfo to ensure we honor time zone
                // changes (e.g. Daylight Savings Time)
                var nowOffset = new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now));
                var nextOffset = new DateTimeOffset(next, TimeZoneInfo.Local.GetUtcOffset(next));
                nextInterval = nextOffset - nowOffset;
            }
            else
            {
                nextInterval = next - now;
            }

            // If the interval happens to be negative (due to slow storage, for example), adjust the
            // interval back up 1 Tick (Zero is invalid for a timer) for an immediate invocation.
            if (nextInterval <= TimeSpan.Zero)
            {
                nextInterval = TimeSpan.FromTicks(1);
            }

            return nextInterval;
        }

        private void StartTimer(TimeSpan interval)
        {
            // Restart the timer with the next schedule occurrence, but only 
            // if Cancel, Stop, and Dispose have not been called.
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

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
