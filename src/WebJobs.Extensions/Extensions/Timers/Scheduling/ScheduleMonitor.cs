// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// This class is used to monitor and record schedule occurrences. It stores
    /// schedule occurrence info to persistent storage at runtime.
    /// <see cref="TimerTriggerAttribute"/> uses this class to monitor
    /// schedules to avoid missing scheduled executions.
    /// </summary>
    public abstract class ScheduleMonitor
    {
        // Recalculate this value every time as our time zone can change dynamically when hosted.
        internal static DateTime DefaultDateTime => DateTime.MinValue.ToLocalTime();

        // We consider anything below this as a "default", unset value. Refactoring to use nullable DateTime would
        // be a disruptive change.
        internal static DateTime DefaultDateTimeThreshold => DefaultDateTime.AddYears(1);

        /// <summary>
        /// Gets the last recorded schedule status for the specified timer.
        /// If the timer has not ran yet, null will be returned.
        /// </summary>
        /// <param name="timerName">The name of the timer to check.</param>
        /// <returns>The schedule status.</returns>
        public abstract Task<ScheduleStatus> GetStatusAsync(string timerName);

        /// <summary>
        /// Gets the last recorded schedule status for the specified timer.
        /// If the timer has not ran yet, null will be returned.
        /// </summary>
        /// <remarks>
        /// The default implementation delegates to <see cref="GetStatusAsync(string)"/> and ignores the
        /// supplied <paramref name="cancellationToken"/>. Override this method to honor cancellation.
        /// </remarks>
        /// <param name="timerName">The name of the timer to check.</param>
        /// <param name="cancellationToken">A token observed for cancellation of the operation.</param>
        /// <returns>The schedule status.</returns>
        public virtual Task<ScheduleStatus> GetStatusAsync(string timerName, CancellationToken cancellationToken)
            => GetStatusAsync(timerName);

        /// <summary>
        /// Internally, calls <see cref="GetStatusAsync(string)"/> and corrects any invalid values
        /// on the returned <see cref="ScheduleStatus"/> object before returning.
        /// </summary>
        /// <param name="timerName">The name of the timer to check.</param>
        /// <returns>The schedule status.</returns>
        public Task<ScheduleStatus> GetSafeStatusAsync(string timerName)
            => GetSafeStatusAsync(timerName, CancellationToken.None);

        /// <summary>
        /// Internally, calls <see cref="GetStatusAsync(string, CancellationToken)"/> and corrects any
        /// invalid values on the returned <see cref="ScheduleStatus"/> object before returning.
        /// </summary>
        /// <param name="timerName">The name of the timer to check.</param>
        /// <param name="cancellationToken">A token observed for cancellation of the operation.</param>
        /// <returns>The schedule status.</returns>
        public async Task<ScheduleStatus> GetSafeStatusAsync(string timerName, CancellationToken cancellationToken)
        {
            var status = await GetStatusAsync(timerName, cancellationToken);

            if (status?.Last < DefaultDateTimeThreshold)
            {
                status.Last = DefaultDateTime;
            }

            if (status?.Next < DefaultDateTimeThreshold)
            {
                status.Next = DefaultDateTime;
            }

            if (status?.LastUpdated < DefaultDateTimeThreshold)
            {
                status.LastUpdated = DefaultDateTime;
            }

            return status;
        }

        /// <summary>
        /// Updates the schedule status for the specified timer.
        /// </summary>
        /// <param name="timerName">The name of the timer.</param>
        /// <param name="status">The new schedule status.</param>
        public abstract Task UpdateStatusAsync(string timerName, ScheduleStatus status);

        /// <summary>
        /// Updates the schedule status for the specified timer.
        /// </summary>
        /// <remarks>
        /// The default implementation delegates to <see cref="UpdateStatusAsync(string, ScheduleStatus)"/>
        /// and ignores the supplied <paramref name="cancellationToken"/>. Override this method to honor
        /// cancellation. Callers should be cautious about cancelling status updates that follow a successful
        /// function invocation, as a failed update can result in duplicate invocations after restart.
        /// </remarks>
        /// <param name="timerName">The name of the timer.</param>
        /// <param name="status">The new schedule status.</param>
        /// <param name="cancellationToken">A token observed for cancellation of the operation.</param>
        public virtual Task UpdateStatusAsync(string timerName, ScheduleStatus status, CancellationToken cancellationToken)
            => UpdateStatusAsync(timerName, status);

        /// <summary>
        /// Checks whether the schedule is currently past due.
        /// </summary>
        /// <remarks>
        /// On startup, all schedules are checked to see if they are past due. Any
        /// timers that are past due will be executed immediately by default. Subclasses can
        /// change this behavior by inspecting the current time and schedule to determine
        /// whether it should be considered past due.
        /// </remarks>
        /// <param name="timerName">The name of the timer to check.</param>
        /// <param name="now">The time to check.</param>
        /// <param name="schedule">The <see cref="TimerSchedule"/>.</param>
        /// <param name="lastStatus">The last recorded status, or null if the status has never been recorded.</param>
        /// <returns>A non-zero <see cref="TimeSpan"/> if the schedule is past due, otherwise <see cref="TimeSpan.Zero"/>.</returns>
        public virtual async Task<TimeSpan> CheckPastDueAsync(string timerName, DateTimeOffset now, TimerSchedule schedule, ScheduleStatus lastStatus)
        {
            DateTimeOffset recordedNextOccurrence;
            if (lastStatus == null)
            {
                // If we've never recorded a status for this timer, write an initial
                // status entry. This ensures that for a new timer, we've captured a
                // status log for the next occurrence even though no occurrence has happened yet
                // (ensuring we don't miss an occurrence)
                DateTimeOffset nextOccurrence = schedule.GetNextOccurrence(now.LocalDateTime);
                lastStatus = new ScheduleStatus
                {
                    Last = DefaultDateTime,
                    Next = nextOccurrence.LocalDateTime,
                    LastUpdated = now.LocalDateTime
                };
                await UpdateStatusAsync(timerName, lastStatus);
                recordedNextOccurrence = nextOccurrence;
            }
            else
            {
                DateTimeOffset expectedNextOccurrence;

                // Track the time that was used to create 'expectedNextOccurrence'.
                DateTimeOffset lastUpdated;

                if (lastStatus.Last > DefaultDateTimeThreshold)
                {
                    // If we have a 'Last' value, we know that we used this to calculate 'Next'
                    // in a previous invocation.
                    expectedNextOccurrence = schedule.GetNextOccurrence(lastStatus.Last);
                    lastUpdated = lastStatus.Last;
                }
                else if (lastStatus.LastUpdated > DefaultDateTimeThreshold)
                {
                    // If the trigger has never fired, we won't have 'Last', but we will have
                    // 'LastUpdated', which tells us the last time that we used to calculate 'Next'.
                    expectedNextOccurrence = schedule.GetNextOccurrence(lastStatus.LastUpdated);
                    lastUpdated = lastStatus.LastUpdated;
                }
                else
                {
                    // If we do not have 'LastUpdated' or 'Last', we don't have enough information to
                    // properly calculate 'Next', so we'll calculate it from the current time.
                    expectedNextOccurrence = schedule.GetNextOccurrence(now.LocalDateTime);
                    lastUpdated = now;
                }

                // ensure that the schedule hasn't been updated since the last
                // time we checked, and if it has, update the status to use the new schedule
                if (lastStatus.Next != expectedNextOccurrence)
                {
                    // if the schedule has changed and the next occurrence is in the past,
                    // recalculate it based on the current time as we don't want it to register
                    // immediately as 'past due'.
                    if (now > expectedNextOccurrence)
                    {
                        expectedNextOccurrence = schedule.GetNextOccurrence(now.LocalDateTime);
                        lastUpdated = now;
                    }

                    lastStatus.Last = DefaultDateTime;
                    lastStatus.Next = expectedNextOccurrence.LocalDateTime;
                    lastStatus.LastUpdated = lastUpdated.LocalDateTime;
                    await UpdateStatusAsync(timerName, lastStatus);
                }
                recordedNextOccurrence = lastStatus.Next;
            }

            if (now > recordedNextOccurrence)
            {
                // if now is after the last next occurrence we recorded, we know we've missed
                // at least one schedule instance and we are past due
                return now - recordedNextOccurrence;
            }
            else
            {
                // not past due
                return TimeSpan.Zero;
            }
        }
    }
}
