// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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
        /// <summary>
        /// Gets the last recorded schedule status for the specified timer.
        /// If the timer has not ran yet, null will be returned.
        /// </summary>
        /// <param name="timerName">The name of the timer to check.</param>
        /// <returns>The schedule status.</returns>
        public abstract Task<ScheduleStatus> GetStatusAsync(string timerName);

        /// <summary>
        /// Updates the schedule status for the specified timer.
        /// </summary>
        /// <param name="timerName">The name of the timer.</param>
        /// <param name="status">The new schedule status.</param>
        public abstract Task UpdateStatusAsync(string timerName, ScheduleStatus status);

        /// <summary>
        /// Determines whether the schedule is currently past due.
        /// </summary>
        /// <remarks>
        /// On startup, all schedules are checked to see if they are past due. Any
        /// timers that are past due will be executed immediately by default. Subclasses can
        /// change this behavior by inspecting the current time and schedule to determine
        /// whether it should be considered past due.
        /// </remarks>
        /// <param name="timerName">The name of the timer to check.</param>
        /// <param name="now">The time to check.</param>
        /// <param name="schedule">The <see cref="TimerSchedule"/></param>
        /// <returns>True if the schedule is past due, false otherwise.</returns>
        public virtual async Task<bool> IsPastDueAsync(string timerName, DateTime now, TimerSchedule schedule)
        {
            TimeSpan pastDueDuration = await GetPastDueDurationAsync(timerName, now, schedule);
            return pastDueDuration != TimeSpan.Zero;
        }

        /// <summary>
        /// Returns the <see cref="TimeSpan"/> duration that the specified timer is past due.
        /// </summary>
        /// <param name="timerName">The name of the timer.</param>
        /// <param name="now">The current time.</param>
        /// <param name="schedule">The <see cref="TimerSchedule"/>.</param>
        /// <returns>The duration the timer is past due.</returns>
        protected async Task<TimeSpan> GetPastDueDurationAsync(string timerName, DateTime now, TimerSchedule schedule)
        {
            ScheduleStatus status = await GetStatusAsync(timerName);
            DateTime recordedNextOccurrence;
            if (status == null)
            {
                // If we've never recorded a status for this timer, write an initial
                // status entry. This ensures that for a new timer, we've captured a
                // status log for the next occurrence even though no occurrence has happened yet
                // (ensuring we don't miss an occurrence)
                DateTime nextOccurrence = schedule.GetNextOccurrence(now);
                status = new ScheduleStatus
                {
                    Last = default(DateTime),
                    Next = nextOccurrence
                };
                await UpdateStatusAsync(timerName, status);
                recordedNextOccurrence = nextOccurrence;
            }
            else
            {
                // ensure that the schedule hasn't been updated since the last
                // time we checked, and if it has, update the status file
                DateTime expectedNextOccurrence;
                if (status.Last == default(DateTime))
                {
                    // there have been no executions of the function yet, so compute
                    // from now
                    expectedNextOccurrence = schedule.GetNextOccurrence(now);
                }
                else
                {
                    // compute the next occurrence from the last
                    expectedNextOccurrence = schedule.GetNextOccurrence(status.Last);
                }

                if (status.Next != expectedNextOccurrence)
                {
                    status.Next = expectedNextOccurrence;
                    await UpdateStatusAsync(timerName, status);
                }
                recordedNextOccurrence = status.Next;
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
